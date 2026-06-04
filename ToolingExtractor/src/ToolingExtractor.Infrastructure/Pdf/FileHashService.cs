using System.Security.Cryptography;

namespace ToolingExtractor.Infrastructure.Pdf;

public class FileHashService
{
    public string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
