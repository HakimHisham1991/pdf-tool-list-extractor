using System.Runtime.Intrinsics.X86;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Infrastructure.Data;

namespace ToolingExtractor.Application.Services;

public class StartupValidationService : IHostedService
{
    private readonly ILogger<StartupValidationService> _logger;
    private readonly ToolingExtractorOptions _options;
    private readonly IServiceProvider _services;

    public StartupValidationService(
        ILogger<StartupValidationService> logger,
        IOptions<ToolingExtractorOptions> options,
        IServiceProvider services)
    {
        _logger = logger;
        _options = options.Value;
        _services = services;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Avx2.IsSupported)
        {
            var cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "unknown";
            Fail($"STARTUP FAILURE: This server's CPU does not support AVX2 instructions, which are required by PaddleSharp MKL-DNN inference. Minimum requirement: Intel Haswell (2013) or AMD Ryzen (2017) or newer. Current CPU: {cpu}");
        }

        ValidateModelFiles();
        ValidateAllowedPaths();
        ValidateOutputDirectories();
        await ValidateDatabaseAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void ValidateModelFiles()
    {
        var subfolders = new[] { "det", "cls", "rec" };
        foreach (var sub in subfolders)
        {
            var dir = Path.Combine(_options.PaddleOcrModelPath, sub);
            var hasModel = File.Exists(Path.Combine(dir, "inference.pdmodel")) ||
                           File.Exists(Path.Combine(dir, "inference.json"));
            var weights = Path.Combine(dir, "inference.pdiparams");
            if (!hasModel || !File.Exists(weights))
            {
                var expectedRoot = Path.GetFullPath(_options.PaddleOcrModelPath);
                Fail($"STARTUP FAILURE: PaddleOCR model files not found at '{dir}'. Expected model root: '{expectedRoot}'. Run scripts/download-models.ps1 from the ToolingExtractor folder, or download manually — see models/ppocr_v4/README.md. The application will NOT download model files at runtime.");
            }
        }
    }

    private void ValidateAllowedPaths()
    {
        if (_options.AllowedBasePaths == null || _options.AllowedBasePaths.Count == 0)
            Fail("STARTUP FAILURE: No AllowedBasePaths configured in appsettings.json. Add at least one allowed folder path under ToolingExtractor:AllowedBasePaths.");
    }

    private void ValidateOutputDirectories()
    {
        foreach (var dir in new[] { "./data", _options.UploadStagingPath, _options.AmendedPdfLogPath, _options.FailedExtractionOutputPath })
        {
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Fail($"STARTUP FAILURE: Cannot create output directory '{dir}': {ex.Message}");
            }
        }
    }

    private async Task ValidateDatabaseAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ToolingDbContext>();
            await db.Database.MigrateAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        }
        catch (Exception ex)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "data", "tooling.db");
            Fail($"STARTUP FAILURE: Cannot access SQLite database at '{path}': {ex.Message}");
        }
    }

    private void Fail(string message)
    {
        _logger.LogCritical("{Message}", message);
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            Environment.Exit(1);
        });
        throw new InvalidOperationException(message);
    }
}
