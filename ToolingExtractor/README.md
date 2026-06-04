# ToolingExtractor v3.3.1

Aerospace-grade CNC tooling PDF extraction system — **100% C# / .NET 10**, with PdfPig (digital text), PaddleSharp OCR (scanned/amended PDFs), SQLite storage, and ASP.NET Core Razor UI.

## Prerequisites

- .NET 10 SDK
- Windows x64 (PaddleSharp MKL-DNN — **AVX2** required: Intel Haswell 2013+ or AMD Ryzen 2017+)
- Native OCR runtime is delivered via NuGet: `Sdcb.PaddleInference.runtime.win64.mkl` (included in the Web project — run `dotnet restore` then `dotnet build`; the Web project copies native DLLs into the output folder on build)
- CPU with AVX2 (startup fails with a clear message if unsupported)

## Model setup (required — no internet download at runtime)

1. Download PP-OCRv4 English models (det, rec, cls) from PaddleOCR releases.
2. Extract to:

```
models/ppocr_v4/
├── det/inference.pdmodel + inference.pdiparams
├── cls/inference.pdmodel + inference.pdiparams
└── rec/inference.pdmodel + inference.pdiparams + en_dict.txt
```

See [models/ppocr_v4/README.md](models/ppocr_v4/README.md).

## Allowed paths

Edit `src/ToolingExtractor.Web/appsettings.json` → `ToolingExtractor:AllowedBasePaths`. Only listed folders can be scanned.

## Database

```powershell
dotnet ef database update --project src/ToolingExtractor.Infrastructure --startup-project src/ToolingExtractor.Web
```

## Run

```powershell
cd ToolingExtractor
dotnet run --project src/ToolingExtractor.Web
```

Open http://localhost:5000 (or the URL shown in the console).

## Workflow (v3.2+)

1. **Extract** — **Import Files** (file browser, multi-select PDFs from any folder) → **Extract Tooling Data** (replaces existing rows for the same file).
2. **Files Processed** — open a file → **Tool List Data Extracted** → export CSV/Excel for that file only.
3. **Export All** — download CSV/Excel for every stored record. **Clear All** on Extract resets database and logs.

Reloading the browser (F5) or opening a new tab also clears data; **Clear All** does the same without leaving the page.

## Tests

```powershell
dotnet test
```

## Output directories (auto-created)

| Path | Purpose |
|------|---------|
| `./data/` | SQLite `tooling.db` |
| `./data/uploads/` | PDFs uploaded via Import Files (cleared on reset) |
| `./amended_pdf_log/` | Amendment JSON reports |
| `./failed_extractions/` | Raw text dumps, `errors.log`, `revision_conflicts.log` |

## Unicode filenames

PDF filenames with Arabic, Chinese, or Malay characters are supported via .NET UTF-16 paths. No locale changes required.

## Third-party licenses (approved)

| Package | License |
|---------|---------|
| PdfPig | Apache-2.0 |
| PdfPig.Rendering.Skia | Apache-2.0 |
| Sdcb.PaddleInference / PaddleOCR | Apache-2.0 |
| SkiaSharp | MIT |
| OpenCvSharp4 | Apache-2.0 |
| ClosedXML | MIT |
| EF Core / Microsoft.Data.Sqlite | MIT |
