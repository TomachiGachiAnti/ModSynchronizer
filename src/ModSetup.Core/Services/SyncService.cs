using ModSetup.Core.Models;

namespace ModSetup.Core.Services;

public sealed class SyncService
{
    private const string StateDirectoryName = ".modsetup";
    private const string ManagedModsFileName = "managed-mods.txt";
    private const string ManagedFilesFileName = "managed-files.txt";

    private readonly PathResolver _pathResolver;
    private readonly DownloadService _downloadService;
    private readonly HashService _hashService;

    public SyncService(PathResolver pathResolver, DownloadService downloadService, HashService hashService)
    {
        _pathResolver = pathResolver;
        _downloadService = downloadService;
        _hashService = hashService;
    }

    public async Task<SetupResult> SyncAsync(
        ProfileConfig profile,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new SetupResult();
        var gameDirectory = _pathResolver.GetGameDirectory(profile);
        Directory.CreateDirectory(gameDirectory);
        Directory.CreateDirectory(_pathResolver.GetModsDirectory(profile));
        var stateDirectory = Path.Combine(gameDirectory, StateDirectoryName);
        Directory.CreateDirectory(stateDirectory);
        var managedModsFilePath = Path.Combine(stateDirectory, ManagedModsFileName);
        var managedFilesFilePath = Path.Combine(stateDirectory, ManagedFilesFileName);
        var previousManagedMods = LoadManagedEntries(managedModsFilePath);
        var previousManagedFiles = LoadManagedEntries(managedFilesFilePath);

        var managedMods = profile.Mods.ToList();
        var activeFiles = profile.Files.Where(x => x.Required).ToList();
        var activeDirectories = profile.Directories
            .Where(x => x.Required || Directory.Exists(ResolveLocalSourcePath(x.SourcePath, x.BundlePath)))
            .ToList();
        var currentManagedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentManagedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = managedMods.Count + activeFiles.Count + CountDirectoryFiles(activeDirectories);
        var current = 0;

        foreach (var mod in managedMods)
        {
            current++;
            progress?.Report(new SetupProgress
            {
                Message = $"MOD を確認しています: {mod.Filename}",
                Current = current,
                Total = total
            });

            var destinationPath = Path.Combine(_pathResolver.GetModsDirectory(profile), mod.Filename);

            if (mod.Deprecated)
            {
                if (profile.Sync.RemoveDeprecatedFiles && File.Exists(destinationPath))
                {
                    File.Delete(destinationPath);
                    result.Mods.Deleted.Add(mod.Filename);
                }

                continue;
            }

            currentManagedMods.Add(mod.Filename);
            var localSourcePath = ResolveLocalSourcePath(mod.SourcePath, mod.BundlePath);
            await SyncFileAsync(
                localSourcePath,
                mod.Url,
                destinationPath,
                mod.Filename,
                mod.Sha256,
                result.Mods,
                cancellationToken);
        }

        foreach (var file in activeFiles)
        {
            current++;
            progress?.Report(new SetupProgress
            {
                Message = $"ファイルを確認しています: {file.Path}",
                Current = current,
                Total = total
            });

            var destinationPath = _pathResolver.GetManagedFilePath(profile, file.Path);
            currentManagedFiles.Add(NormalizeManagedPath(file.Path));
            var localSourcePath = ResolveLocalSourcePath(file.SourcePath, file.BundlePath);
            await SyncFileAsync(
                localSourcePath,
                file.Url,
                destinationPath,
                file.Path,
                file.Sha256,
                result.Files,
                cancellationToken);
        }

        foreach (var directory in activeDirectories)
        {
            current = await SyncDirectoryAsync(
                profile,
                directory,
                result.Files,
                currentManagedFiles,
                current,
                total,
                progress,
                cancellationToken);
        }

        PersistManagedEntries(managedModsFilePath, currentManagedMods);
        PersistManagedEntries(managedFilesFilePath, currentManagedFiles);

        if (profile.Sync.RemoveManagedFilesNotInManifest)
        {
            RemoveMissingManagedMods(profile, previousManagedMods, currentManagedMods, result.Mods);
            RemoveMissingManagedFiles(profile, previousManagedFiles, currentManagedFiles, result.Files);
        }

        return result;
    }

    private async Task SyncFileAsync(
        string sourcePath,
        string url,
        string destinationPath,
        string displayName,
        string sha256,
        SyncItemResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) && string.IsNullOrWhiteSpace(url))
        {
            result.Failed.Add(displayName);
            return;
        }

        var fileExists = File.Exists(destinationPath);
        var isValid = fileExists && _hashService.VerifySha256(destinationPath, sha256);

        if (isValid)
        {
            result.Skipped.Add(displayName);
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                CopyLocalFile(sourcePath, destinationPath);
            }
            else
            {
                await _downloadService.DownloadFileAsync(url, destinationPath, cancellationToken);
            }

            if (!_hashService.VerifySha256(destinationPath, sha256))
            {
                result.Failed.Add(displayName);
                return;
            }

            if (fileExists)
            {
                result.Updated.Add(displayName);
            }
            else
            {
                result.Downloaded.Add(displayName);
            }
        }
        catch
        {
            result.Failed.Add(displayName);
        }
    }

    private static void CopyLocalFile(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("コピー元ファイルが見つかりません。", sourcePath);
        }

        var directoryPath = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.Copy(sourcePath, destinationPath, true);
    }

    private async Task<int> SyncDirectoryAsync(
        ProfileConfig profile,
        ManagedDirectoryEntry directory,
        SyncItemResult result,
        HashSet<string> currentManagedFiles,
        int current,
        int total,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sourceDirectoryPath = ResolveLocalSourcePath(directory.SourcePath, directory.BundlePath);

        if (!Directory.Exists(sourceDirectoryPath))
        {
            if (directory.Required)
            {
                result.Failed.Add(directory.Path);
            }

            return current;
        }

        var files = Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var sourceFilePath in files)
        {
            current++;

            var relativePath = Path.GetRelativePath(sourceDirectoryPath, sourceFilePath);
            var destinationRelativePath = Path.Combine(directory.Path, relativePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var destinationPath = _pathResolver.GetManagedFilePath(profile, destinationRelativePath);
            currentManagedFiles.Add(NormalizeManagedPath(destinationRelativePath));

            progress?.Report(new SetupProgress
            {
                Message = $"ディレクトリ内ファイルを確認しています: {destinationRelativePath}",
                Current = current,
                Total = total
            });

            await SyncFileAsync(
                sourceFilePath,
                "",
                destinationPath,
                destinationRelativePath,
                "",
                result,
                cancellationToken);
        }

        return current;
    }

    private int CountDirectoryFiles(IEnumerable<ManagedDirectoryEntry> directories)
    {
        var total = 0;

        foreach (var directory in directories)
        {
            var sourceDirectoryPath = ResolveLocalSourcePath(directory.SourcePath, directory.BundlePath);
            if (!Directory.Exists(sourceDirectoryPath))
            {
                continue;
            }

            total += Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories).Length;
        }

        return total;
    }

    private string ResolveLocalSourcePath(string sourcePath, string bundlePath)
    {
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            return sourcePath;
        }

        if (!string.IsNullOrWhiteSpace(bundlePath))
        {
            return _pathResolver.ResolveBundlePath(bundlePath);
        }

        return "";
    }

    private static HashSet<string> LoadManagedEntries(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return File.ReadAllLines(filePath)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void PersistManagedEntries(string filePath, HashSet<string> entries)
    {
        var orderedEntries = entries
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(filePath, orderedEntries);
    }

    private void RemoveMissingManagedMods(
        ProfileConfig profile,
        HashSet<string> previousManagedMods,
        HashSet<string> currentManagedMods,
        SyncItemResult result)
    {
        foreach (var previousManagedMod in previousManagedMods)
        {
            if (currentManagedMods.Contains(previousManagedMod))
            {
                continue;
            }

            var destinationPath = Path.Combine(_pathResolver.GetModsDirectory(profile), previousManagedMod);
            if (!File.Exists(destinationPath))
            {
                continue;
            }

            File.Delete(destinationPath);
            result.Deleted.Add(previousManagedMod);
        }
    }

    private void RemoveMissingManagedFiles(
        ProfileConfig profile,
        HashSet<string> previousManagedFiles,
        HashSet<string> currentManagedFiles,
        SyncItemResult result)
    {
        foreach (var previousManagedFile in previousManagedFiles)
        {
            if (currentManagedFiles.Contains(previousManagedFile))
            {
                continue;
            }

            var destinationPath = _pathResolver.GetManagedFilePath(profile, previousManagedFile);
            if (!File.Exists(destinationPath))
            {
                continue;
            }

            File.Delete(destinationPath);
            result.Deleted.Add(previousManagedFile);
        }
    }

    private static string NormalizeManagedPath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }
}
