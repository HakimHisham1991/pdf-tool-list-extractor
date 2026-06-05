# PDF Tool List Extractor (ToolingExtractor)

**Current version: v3.10.4**

Monorepo for **ToolingExtractor** — a **.NET 10** web application that extracts aerospace CNC tooling tables from PDF work instructions and tooling lists, stores them in **SQLite**, and provides CSV/Excel export. An optional **Python** bordered-table engine improves Master Tooling List PDFs (Template A).

| Capability | Technology |
|------------|------------|
| **Bordered tool tables (Template A)** | **pdfplumber** → **camelot** → **tabula** (Python subprocess) |
| Digital / embedded text PDFs | [PdfPig](https://github.com/UglyToad/PdfPig) |
| Scanned & amended PDFs | [PaddleSharp](https://github.com/sdcb/PaddleSharp) OCR (PP-OCRv4, MKL-DNN) |
| Storage | SQLite + Entity Framework Core |
| Export | CSV and Excel (same columns, PDF row order) |
| **PDF page preview (UI)** | [PDFtoImage](https://www.nuget.org/packages/PDFtoImage/) / **PDFium** |
| UI | ASP.NET Core Razor Pages + Tailwind (CDN) |

**Release notes:** [CHANGELOG.md](CHANGELOG.md)  
**Python module details:** [python/README.md](python/README.md)

---

## Table of contents

1. [What it does](#what-it-does)
2. [Repository layout](#repository-layout)
3. [Prerequisites](#prerequisites)
4. [Quick start](#quick-start)
5. [OCR model setup](#ocr-model-setup)
6. [Configuration reference](#configuration-reference)
7. [Using the web UI](#using-the-web-ui)
8. [Session and data lifecycle](#session-and-data-lifecycle)
9. [Python table extraction (Template A)](#python-table-extraction-template-a)
10. [How PDFs are processed](#how-pdfs-are-processed)
11. [Tooling templates](#tooling-templates)
12. [REST API](#rest-api)
13. [Output folders](#output-folders)
14. [Troubleshooting](#troubleshooting)
15. [Development](#development)
16. [Third-party licenses](#third-party-licenses)

---

## What it does

ToolingExtractor automates extraction of tooling tables from PDF work instructions and tooling lists used in aerospace CNC workflows. For each PDF it can determine:

- **Header metadata** — part number, operation, revision, machine, tool list ID (filename), etc.
- **One row per tool** — tool number, name, holder, diameters, lengths, supplier, path time, remarks, and related columns.
- **PDF characteristics** — digital vs scanned, amendment overlays, template type (A/B/C), OCR confidence, revision conflicts.

Results are browsable in the UI, exportable per file or in bulk, and tied to a stable **file content hash** so re-running extraction on the same file replaces prior rows (re-extract is always enabled).

---

## Repository layout

```
pdf-tool-list-extractor/
├── run.bat                         # Start the web app (Windows)
├── python/                         # pdfplumber / camelot / tabula table extractor
│   └── table_extractor/
├── ToolingExtractor/               # Main .NET solution
│   ├── src/
│   │   ├── ToolingExtractor.Core/
│   │   ├── ToolingExtractor.Infrastructure/
│   │   ├── ToolingExtractor.Application/
│   │   └── ToolingExtractor.Web/   # Razor UI + REST API (run from here)
│   ├── tests/
│   ├── models/ppocr_v4/            # Paddle OCR models (local, not always in git)
│   └── scripts/download-models.ps1
├── CHANGELOG.md
└── README.md                       # This file
```

**Layering**

- **Web** — HTTP endpoints and pages; no business rules.
- **Application** — `ExtractionPipelineService`, `ExtractionJobQueue`, `PdfUploadService`, `DataResetService`, `FolderScanService`.
- **Infrastructure** — SQLite, PdfPig, PaddleOCR, `ParserA`/`ParserB`/`ParserC`, CSV/Excel export.
- **Core** — `ToolingRecord`, `ExtractionJob`, configuration options.

---

## Prerequisites

| Requirement | Notes |
|-------------|--------|
| **.NET 10 SDK** | `dotnet --version` should show 10.x |
| **Windows x64** | PaddleSharp MKL runtime is Windows-focused in this solution |
| **AVX2 CPU** | Intel Haswell (2013+) or AMD Ryzen (2017+). Startup fails with a clear message if missing |
| **Disk space** | ~200 MB for OCR models; additional space for uploads and SQLite |
| **RAM** | Large landscape WI sheets use capped page rendering (`MaxPageRenderPixels`) to avoid OOM; 8 GB+ recommended for heavy OCR batches |
| **Python 3.10+** (recommended) | For bordered Master Tooling List table extraction; falls back to text parsing if missing |
| **Ghostscript** (optional) | Required for camelot `lattice` mode on some PDFs |

Native OCR DLLs come from NuGet package `Sdcb.PaddleInference.runtime.win64.mkl`. After `dotnet restore` and `dotnet build`, native libraries are copied into the Web project output directory.

---

## Quick start

### Option A — `run.bat` (Windows)

From the repository root, double-click **`run.bat`** or run:

```bat
run.bat
```

This starts `dotnet run` in `ToolingExtractor\src\ToolingExtractor.Web`.

### Option B — Manual

```powershell
cd ToolingExtractor
dotnet restore
dotnet build

# OCR models (required for scanned PDFs) — one time
.\scripts\download-models.ps1

# Python table extractor (strongly recommended for Template A WI PDFs)
cd ..\python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r table_extractor\requirements.txt
cd ..\ToolingExtractor

# Apply database schema (first run also migrates on startup)
dotnet ef database update `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web

dotnet run --project src/ToolingExtractor.Web
```

Open the URL printed in the console (often `http://localhost:5000` or `http://localhost:5261`).

**Minimal workflow**

1. **Extract** → **Import Files** (multi-select PDFs) → **Extract Tooling Data**
2. **Files Processed** → open a filename link → view tools or export per file
3. **Export All** → download everything as CSV/Excel

---

## OCR model setup

OCR models are **not** downloaded at runtime. Install them once before scanned/amended PDF extraction works.

### Option A — PowerShell script (recommended)

```powershell
cd ToolingExtractor
.\scripts\download-models.ps1
```

This populates `ToolingExtractor/models/ppocr_v4/` with `det`, `cls`, and `rec` inference folders.

### Option B — Manual layout

```
ToolingExtractor/models/ppocr_v4/
├── det/
├── cls/
└── rec/          # must include en_dict.txt
```

Each subfolder needs `inference.pdmodel` (or `inference.json`) and `inference.pdiparams`. See `ToolingExtractor/models/ppocr_v4/README.md` if present.

---

## Configuration reference

Edit **`ToolingExtractor/src/ToolingExtractor.Web/appsettings.json`** → section `ToolingExtractor`.

| Setting | Default | Description |
|---------|---------|-------------|
| `AllowedBasePaths` | `C:\Tools\PDFs`, … | Folders allowed for legacy folder scan API. Not required for browser Import Files. |
| `UploadStagingPath` | `./data/uploads` | Uploaded PDFs per import batch. Cleared on data reset. |
| `MaxUploadFileBytes` | `209715200` (200 MB) | Maximum size per uploaded PDF. |
| `PaddleOcrModelPath` | `./models/ppocr_v4` | OCR model root. |
| `PdfRenderDpi` | `300` | Rasterization DPI for OCR path. |
| `MaxPageRenderPixels` | `4000` | Longest edge cap per page (prevents Skia OOM). |
| `PerFileTimeoutSeconds` | `600` | Per-PDF processing timeout. |
| `MaxParallelFiles` | `4` | Parallel PDF workers on local disks. |
| `NetworkPathThrottleParallelism` | `2` | Parallelism for UNC/network drives. |
| `OcrConfidenceThreshold` | `0.6` | OCR confidence threshold. |
| `FailedExtractionOutputPath` | `./failed_extractions` | Failed parse dumps and `errors.log`. |
| `AmendedPdfLogPath` | `./amended_pdf_log` | Amendment detection JSON logs. |
| `PythonTableExtraction:Enabled` | `true` | Run Python pdfplumber/camelot/tabula for Template A. |
| `PythonTableExtraction:PythonExecutable` | `python` | Python on PATH (or venv python path). |
| `PythonTableExtraction:TimeoutSeconds` | `180` | Subprocess timeout per PDF. |
| `PythonTableExtraction:MinimumToolRows` | `3` | Minimum valid tool rows (`T##` or numeric, e.g. `10`). |

**Logging** — `ToolingExtractor` namespace defaults to `Debug` in appsettings during development.

**Upload size (server)** — Kestrel allows multi-file uploads up to **2 GB** total per request.

---

## Using the web UI

### Dashboard (`/`)

Summary counts: total records, digital vs scanned, amended PDFs, revision conflicts.

### Extract (`/Extract`)

| Control | Action |
|---------|--------|
| **Import Files** | Always active. Opens the OS file picker; uploaded PDFs appear in a table (No., Filename, **eye icon**). |
| **Eye icon (preview)** | Full-page JPEG below the import table. Multi-page PDFs: use ‹ › under the filename. Highlights persist per file. |
| **Extract Tooling Data** | Progress bar, log, and elapsed timer (`HH:MM:SS`). Always re-extracts matching file hashes. |
| **Stop** | Cancels the running job; keeps the current preview. |
| **Clear All** | Clears the import list and preview; resets the elapsed timer to `00:00:00`. |

**Import tips** — Multi-select with Ctrl/Shift. Duplicate filenames in one batch are renamed (`file_2.pdf`, etc.). Hard-refresh (Ctrl+F5) if the picker seems unresponsive.

### Files Processed (`/FilesProcessed`)

One row per extracted PDF: **ToolListId** shows the original filename (link to tool rows), plus part, operation, revision. **Edit** metadata; **Delete** one file; **Clear Files** removes all processed files and highlight overlays. Filter by part/operation.

### Tool List Data Extracted (`/ToolListData?hash=…`)

Full tooling table for a single file. **Download CSV** / **Download Excel** for that file only.

### Export All (`/ExportAll`)

Downloads all tooling records as CSV or Excel. Legacy URL `/Export` redirects here.

---

## Session and data lifecycle

| Event | What gets cleared |
|--------|-------------------|
| **Browser reload (F5)** | SQLite DB recreated, `failed_extractions`, `amended_pdf_log`, `data/uploads`, Extract workspace |
| **New tab or window** | Same as reload |
| **Close tab or window** | Session cookie expires; next open is a new tab (full reset) |
| **Navigate between pages** (same tab) | Data **kept** — session cookie preserves DB rows and Extract workspace |

Finish extraction and export before F5 or closing the tab, or you will need to import and extract again.

---

## Python table extraction (Template A)

For **Master Tooling List** PDFs with ruled borders, the app prefers a Python pipeline that preserves column alignment.

### Strategy order

1. **pdfplumber-wordgrid** — vertical PDF rules → 12 columns (best for landscape Master Tooling List)
2. **pdfplumber** — line-based table detection
3. **camelot** — `lattice` then `stream` (Ghostscript for lattice)
4. **tabula-py** — lattice/stream fallback

### Output columns

| Python column | Stored in DB as |
|---------------|-----------------|
| `Tool_No` | Tool No. |
| `Tool_Name` | Tool Name |
| `Consumable_Tool_Description` | Consumable Tool Description |
| `Tool_Supplier` | Tool Supplier |
| `Tool_Identifier` | Tool Holder |
| `Total_Diameter` | Tool Diameter (D1) |
| `Flute_Length` | Flute Length (L1) |
| `Total_Length` | Tool Ext. Length (L2) |
| `Total_Corner_Radius` | Tool Corner Radius |
| `Anchor_Description` | Arbor Description |
| `Tool_Path_Time_In_Minutes` | Tool Path Time in Minutes |
| `Remarks` | Remarks |

Cleaning includes footer/stamp removal, tool number validation (`T01` or SECO numeric `10`, `11`, …), decimal repair, Ø/° symbol normalisation, and continuation-row merge.

### Standalone test

```powershell
cd python
.\.venv\Scripts\Activate.ps1
python -m table_extractor --pdf "C:\Tools\PDFs\SAMPLE.pdf"
```

Set `ToolingExtractor:PythonTableExtraction:Enabled` to `false` in appsettings to use only C# parsers.

---

## How PDFs are processed

```
Upload batch (staging folder)
    → For each PDF (parallel, per-file timeout):
        → SHA-256 hash → delete old rows if re-extracting
        → Classify (Digital / Scanned / Mixed / Amended)
        → Extract text (PdfPig and/or PaddleOCR)
        → Detect template A, B, or C
        → Template A: Python table extract OR fallback text parse
        → Templates B/C: header + tool rows from text
        → Amendment flag, revision conflict check
    → Bulk insert ToolingRecord rows → update job status
```

**Failed files** — Check `failed_extractions/<filename>.pdf.txt` and `errors.log`.

---

## Tooling templates

| Template | Typical documents |
|----------|-------------------|
| **A** | Master Tooling List / WI variants (landscape single-line tables, SECO stamped lists) |
| **B** | Template B tooling list layouts |
| **C** | Template C tooling list layouts |

Detection is automatic. Unknown templates count as **failed** for that file.

---

## REST API

Base URL: same origin as the web app (e.g. `http://localhost:5261`).

### Extraction

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/files/import` | Multipart `files` — upload PDFs; returns `{ folderPath, batchId, count, files[] }`. |
| `GET` | `/api/files/preview/meta` | Query `folderPath`, `relativePath` → page count. |
| `GET` | `/api/files/preview` | Full-page JPEG preview. |
| `POST` | `/api/extraction/start` | Start job; returns `{ jobId }`. |
| `POST` | `/api/extraction/{jobId}/stop` | Cancel running job. |
| `GET` | `/api/extraction/status/{jobId}` | Job progress and counts. |
| `GET` | `/api/extraction/{jobId}/visualizer` | Live extraction snapshot + highlights. |
| `GET` | `/api/files/highlights` | Persisted highlight overlays per file/page. |

### Files and records

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/files` | Paginated processed-file summaries. |
| `GET` | `/api/files/{hash}` | File summary + tool rows. |
| `PATCH` | `/api/files/{hash}` | Update metadata. |
| `DELETE` | `/api/files/all` | Delete all processed files. |
| `DELETE` | `/api/files/{hash}` | Delete one file by hash. |
| `GET` | `/api/records` | Paginated raw records. |

### Export

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/export/csv` | All records, or `?jobId=`, or `?hash=`. |
| `GET` | `/api/export/excel` | Same data and columns as CSV; single **Tool Records** sheet. |

### Admin / session

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard` | Dashboard stats. |
| `POST` | `/api/session/reload` | Reset DB on browser reload. |
| `GET` / `POST` | `/api/session/workspace` | Extract page workspace persistence. |

---

## Output folders

Paths are relative to the web working directory (`ToolingExtractor/src/ToolingExtractor.Web` when using `dotnet run`).

| Path | Purpose |
|------|---------|
| `./data/tooling.db` | SQLite database |
| `./data/uploads/{batchId}/` | Uploaded PDFs per import batch |
| `./failed_extractions/` | Failed parse dumps, `errors.log` |
| `./amended_pdf_log/` | Amendment JSON per file |

All except the database file are emptied on session reset; the database is dropped and recreated via EF migrations.

---

## Troubleshooting

| Symptom | Things to check |
|---------|------------------|
| Startup: AVX2 error | CPU too old for MKL-DNN. |
| Startup: Paddle DLL missing | `dotnet restore`, rebuild; verify `paddle_inference*.dll` in output. |
| Startup: model error | Run `ToolingExtractor\scripts\download-models.ps1`. |
| Import does nothing | Hard-refresh (Ctrl+F5). Check browser console. |
| Tool columns jumbled | Install Python deps: `pip install -r python/table_extractor/requirements.txt`. |
| Ø / ° symbols wrong | Re-extract on v3.9.9+ (UTF-8 Python bridge + symbol normalisation). |
| SECO stamped PDF: header only | Re-extract on v3.9.6+ (numeric tool numbers `10`, `11`, …). |
| Extract: timeout | Increase `PerFileTimeoutSeconds`. |
| Extract: file locked | Close PDF in Foxit/Adobe. |
| MSB3027 / DLL locked on build | Stop running web app, then rebuild. |

**Logs** — Watch the console where `dotnet run` or `run.bat` is active.

---

## Development

```powershell
cd ToolingExtractor
dotnet test

dotnet ef migrations add <Name> `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web

dotnet ef database update `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web
```

**Version** — Shown in the sidebar (`_Layout.cshtml`). Release notes: [CHANGELOG.md](CHANGELOG.md).

---

## Third-party licenses

| Component | License |
|-----------|---------|
| PdfPig / PdfPig.Rendering.Skia | Apache-2.0 |
| Sdcb.PaddleInference / PaddleOCR | Apache-2.0 |
| SkiaSharp | MIT |
| OpenCvSharp4 | Apache-2.0 |
| ClosedXML | MIT |
| EF Core / Microsoft.Data.Sqlite | MIT |

---

## Unicode filenames

PDF paths and filenames with Arabic, Chinese, Malay, or other Unicode characters are supported via .NET UTF-16 APIs. No special Windows locale configuration is required.
