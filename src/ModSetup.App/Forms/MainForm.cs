using ModSetup.Core.Models;
using ModSetup.Core.Services;
using ModSetup.App.Services;

namespace ModSetup.App.Forms;

public sealed class MainForm : Form
{
    private readonly ComboBox _profileComboBox;
    private readonly Button _refreshButton;
    private readonly Button _runButton;
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;
    private readonly ProfileCatalogService _profileCatalogService;
    private readonly SetupRunner _setupRunner;
    private readonly SelfUpdateService _selfUpdateService;

    public MainForm()
    {
        Text = "Mod Setup";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 220);

        var httpClient = new HttpClient();
        var pathResolver = new PathResolver();
        var downloadService = new DownloadService(httpClient);
        var hashService = new HashService();
        var profileLoader = new ProfileLoader();
        var minecraftEnvironmentService = new MinecraftEnvironmentService(pathResolver);
        var loaderPreparationService = new LoaderPreparationService(downloadService, new JavaRuntimeResolver(), hashService);
        var syncService = new SyncService(pathResolver, downloadService, hashService);
        _selfUpdateService = new SelfUpdateService(httpClient, downloadService, hashService);
        _profileCatalogService = new ProfileCatalogService(httpClient, profileLoader);
        _setupRunner = new SetupRunner(
            profileLoader,
            pathResolver,
            minecraftEnvironmentService,
            loaderPreparationService,
            syncService,
            new LauncherService());

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 3,
            RowCount = 4
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            AutoSize = true,
            Text = "構成ファイルを選択してセットアップを実行します。"
        };

        _profileComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        _refreshButton = new Button
        {
            AutoSize = true,
            Text = "再読込"
        };
        _refreshButton.Click += async (_, _) => await LoadProfilesAsync();

        _runButton = new Button
        {
            AutoSize = true,
            Text = "セットアップ開始"
        };
        _runButton.Click += async (_, _) => await RunSetupAsync();

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100
        };

        _statusLabel = new Label
        {
            AutoSize = true,
            Text = "待機中です。"
        };

        rootLayout.Controls.Add(titleLabel, 0, 0);
        rootLayout.SetColumnSpan(titleLabel, 3);
        rootLayout.Controls.Add(_profileComboBox, 0, 1);
        rootLayout.Controls.Add(_refreshButton, 1, 1);
        rootLayout.Controls.Add(_runButton, 2, 1);
        rootLayout.Controls.Add(_progressBar, 0, 2);
        rootLayout.SetColumnSpan(_progressBar, 3);
        rootLayout.Controls.Add(_statusLabel, 0, 3);
        rootLayout.SetColumnSpan(_statusLabel, 3);

        Controls.Add(rootLayout);

        Shown += async (_, _) => await LoadProfilesAsync();
    }

    private async Task LoadProfilesAsync()
    {
        _refreshButton.Enabled = false;
        _runButton.Enabled = false;
        _statusLabel.Text = "構成ファイルを確認しています。";

        _profileComboBox.BeginUpdate();
        _profileComboBox.Items.Clear();

        try
        {
            var entries = await _profileCatalogService.LoadAvailableProfilesAsync(CancellationToken.None);
            foreach (var entry in entries)
            {
                _profileComboBox.Items.Add(new ProfileItem(entry.ProfileName, entry.DisplayName, entry.Path, entry.IsRemote));
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "構成ファイルの取得に失敗しました。";
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _profileComboBox.EndUpdate();
            _refreshButton.Enabled = true;
        }

        if (_profileComboBox.Items.Count > 0)
        {
            _profileComboBox.SelectedIndex = 0;
            _statusLabel.Text = "構成ファイルを選択できます。";
            _runButton.Enabled = true;
        }
        else
        {
            _runButton.Enabled = false;
            _statusLabel.Text = "利用可能な構成ファイルがありません。";
        }
    }

    private async Task RunSetupAsync()
    {
        if (_profileComboBox.SelectedItem is not ProfileItem item)
        {
            MessageBox.Show(this, "構成ファイルを選択してください。", "確認", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _runButton.Enabled = false;
        _refreshButton.Enabled = false;
        _progressBar.Value = 0;
        _statusLabel.Text = "セットアップを開始します。";

        try
        {
            var resolvedItem = await _profileCatalogService.RefreshAsync(
                new ProfileCatalogEntry(item.ProfileName, item.DisplayName, item.Path, item.IsRemote),
                CancellationToken.None);
            var profile = _setupRunner.LoadProfile(resolvedItem.Path);
            _statusLabel.Text = "自己更新を確認しています。";
            var selfUpdateResult = await _selfUpdateService.CheckAndApplyAsync(profile, CancellationToken.None);
            if (selfUpdateResult.UpdateScheduled)
            {
                _statusLabel.Text = "アプリを更新しています。";
                MessageBox.Show(
                    this,
                    $"新しい版 {selfUpdateResult.LatestVersion} を適用します。アプリを閉じて更新後に自動で再起動します。",
                    "更新",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                BeginInvoke(new Action(Close));
                return;
            }

            var progress = new Progress<SetupProgress>(UpdateProgress);
            var result = await _setupRunner.RunAsync(resolvedItem.Path, progress, CancellationToken.None);
            _statusLabel.Text = "セットアップが完了しました。";
            MessageBox.Show(this, BuildSummary(result), "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "セットアップに失敗しました。";
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runButton.Enabled = true;
            _refreshButton.Enabled = true;
        }
    }

    private void UpdateProgress(SetupProgress progress)
    {
        _statusLabel.Text = progress.Message;

        if (progress.Total <= 0)
        {
            _progressBar.Value = 0;
            return;
        }

        var value = (int)Math.Round(progress.Current * 100d / progress.Total);
        _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, value));
    }

    private static string BuildSummary(SetupResult result)
    {
        return string.Join(
            Environment.NewLine,
            [
                $"MOD ダウンロード: {result.Mods.Downloaded.Count}",
                $"MOD 更新: {result.Mods.Updated.Count}",
                $"MOD 削除: {result.Mods.Deleted.Count}",
                $"ファイル ダウンロード: {result.Files.Downloaded.Count}",
                $"ファイル 更新: {result.Files.Updated.Count}",
                $"警告: {result.Warnings.Count}"
            ]);
    }

    private sealed record ProfileItem(string ProfileName, string DisplayName, string Path, bool IsRemote)
    {
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
