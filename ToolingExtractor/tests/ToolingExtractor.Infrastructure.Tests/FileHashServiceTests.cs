using ToolingExtractor.Infrastructure.Pdf;
using Xunit;

namespace ToolingExtractor.Infrastructure.Tests;

public class FileHashServiceTests
{
    [Fact]
    public void ComputeSha256_SameFileContent_SameHash()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "tooling-test-content");
            var svc = new FileHashService();
            var h1 = svc.ComputeSha256(path);
            var h2 = svc.ComputeSha256(path);
            Assert.Equal(h1, h2);
            Assert.Equal(64, h1.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
