using System.Security.Cryptography;

namespace ModSynchronizer.Core.Services;

public sealed class HashService
{
    public bool VerifySha256(string filePath, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            return true;
        }

        if (!File.Exists(filePath))
        {
            return false;
        }

        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        var actual = Convert.ToHexString(hash);
        return string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
