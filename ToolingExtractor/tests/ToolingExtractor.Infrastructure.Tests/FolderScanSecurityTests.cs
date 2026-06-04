using System.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToolingExtractor.Application.Services;
using ToolingExtractor.Core.Configuration;
using Xunit;

namespace ToolingExtractor.Infrastructure.Tests;

public class FolderScanSecurityTests
{
    private FolderScanService CreateService(params string[] allowed)
    {
        var opts = Options.Create(new ToolingExtractorOptions
        {
            AllowedBasePaths = allowed.ToList()
        });
        return new FolderScanService(opts, NullLogger<FolderScanService>.Instance);
    }

    [Fact]
    public void ValidateFolderPath_AllowedPath_NoException()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());
        var svc = CreateService(temp);
        svc.ValidateFolderPath(temp);
    }

    [Fact]
    public void ValidateFolderPath_TraversalAttempt_ThrowsSecurityException()
    {
        var allowed = Path.GetFullPath(Path.GetTempPath());
        var svc = CreateService(allowed);
        Assert.Throws<SecurityException>(() => svc.ValidateFolderPath(@"C:\Windows\System32"));
    }

    [Fact]
    public void ValidateFolderPath_UnconfiguredAllowlist_ThrowsSecurityException()
    {
        var svc = CreateService();
        Assert.Throws<SecurityException>(() => svc.ValidateFolderPath(Path.GetTempPath()));
    }

    [Fact]
    public void ValidateFolderPath_PathNotInAllowlist_MessageDoesNotContainPath()
    {
        var allowed = Path.GetFullPath(Path.GetTempPath());
        var svc = CreateService(allowed);
        var ex = Assert.Throws<SecurityException>(() => svc.ValidateFolderPath(@"D:\Secret\Tools"));
        Assert.DoesNotContain(@"D:\Secret", ex.Message);
    }
}
