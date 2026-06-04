using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using ModelVersion = Sdcb.PaddleOCR.Models.ModelVersion;
using ToolingExtractor.Application;
using ToolingExtractor.Core.Configuration;
using ToolingExtractor.Application.Services;
using ToolingExtractor.Core.Interfaces;
using ToolingExtractor.Infrastructure.Data;
using ToolingExtractor.Infrastructure.Data.Repositories;
using ToolingExtractor.Infrastructure.Export;
using ToolingExtractor.Infrastructure.Parsing;
using ToolingExtractor.Infrastructure.Pdf;
using ToolingExtractor.Web;
using ToolingExtractor.Web.Api;

PaddleNativeBootstrap.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<ToolingExtractorOptions>()
    .Bind(builder.Configuration.GetSection(ToolingExtractorOptions.SectionName))
    .PostConfigure(ToolingPaths.Normalize);

var contentRoot = ToolingPaths.ResolveContentPath("./data");
Directory.CreateDirectory(contentRoot);
var dbPath = Path.Combine(contentRoot, "tooling.db");
builder.Services.AddDbContext<ToolingDbContext>(o =>
    o.UseSqlite($"Data Source={dbPath};Cache=Shared;"));

builder.Services.AddHostedService<StartupValidationService>();

builder.Services.AddSingleton<PaddleOcrAll>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ToolingExtractorOptions>>().Value;
    var detDir = Path.Combine(opts.PaddleOcrModelPath, "det");
    var clsDir = Path.Combine(opts.PaddleOcrModelPath, "cls");
    var recDir = Path.Combine(opts.PaddleOcrModelPath, "rec");
    var model = new FullOcrModel(
        DetectionModel.FromDirectory(detDir, ModelVersion.V4),
        ClassificationModel.FromDirectory(clsDir, ModelVersion.V4),
        RecognizationModel.FromDirectory(recDir, Path.Combine(recDir, "en_dict.txt"), ModelVersion.V4));
    return new PaddleOcrAll(model, PaddleDevice.Mkldnn());
});

builder.Services.AddSingleton<FileHashService>();
builder.Services.AddScoped<IAmendmentDetector, AmendmentDetector>();
builder.Services.AddScoped<IPdfClassifier, PdfClassifier>();
builder.Services.AddScoped<DigitalPdfExtractor>();
builder.Services.AddScoped<ScannedPdfExtractor>();
builder.Services.AddScoped<ImagePreprocessor>();
builder.Services.AddScoped<OcrTextCorrector>();
builder.Services.AddScoped<ITemplateDetector, TemplateDetector>();
builder.Services.AddScoped<ParserA>();
builder.Services.AddScoped<IToolingParser, ParserA>();
builder.Services.AddScoped<IToolingParser, ParserB>();
builder.Services.AddScoped<IToolingParser, ParserC>();
builder.Services.AddScoped<ParserFactory>();
builder.Services.AddScoped<PythonTableExtractionBridge>();
builder.Services.AddScoped<TemplateAParseService>();
builder.Services.AddScoped<ExtractionPipelineService>();
builder.Services.AddSingleton<ExtractionVisualizerStore>();
builder.Services.AddSingleton<IExtractionVisualizerNotifier>(sp =>
    sp.GetRequiredService<ExtractionVisualizerStore>());
builder.Services.AddSingleton<PdfPagePreviewService>();
builder.Services.AddSingleton<ExtractionJobQueue>();
builder.Services.AddScoped<FolderScanService>();
builder.Services.AddScoped<PdfUploadService>();
builder.Services.AddScoped<DataResetService>();

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 2L * 1024 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024);
builder.Services.AddScoped<ToolingRepository>();
builder.Services.AddScoped<CsvExportService>();
builder.Services.AddScoped<ExcelExportService>();

builder.Services.AddRazorPages();

var app = builder.Build();

try
{
    _ = app.Services.GetRequiredService<PaddleOcrAll>();
}
catch (DllNotFoundException ex)
{
    Console.Error.WriteLine(
        "STARTUP FAILURE: PaddleOCR native runtime DLLs are missing. " +
        "Restore NuGet packages (Sdcb.PaddleInference.runtime.win64.mkl) and rebuild. " +
        ex.Message);
    Environment.Exit(1);
}

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapExtractionApi();

app.Run();
