# ToolingExtractor v3.6.1

Production-oriented **aerospace CNC tooling PDF extraction** — **.NET 10** web app plus an optional **Python** bordered-table engine for Master Tooling List PDFs. It reads Work Instruction / tooling list PDFs, extracts structured tool rows, stores them in **SQLite**, and exposes a **Razor Pages** web UI plus JSON/CSV/Excel export APIs.

| Capability | Technology |
|------------|------------|
| **Bordered tool tables (Template A)** | **pdfplumber** → **camelot** → **tabula** (Python, via subprocess) |
| Digital / embedded text PDFs | [PdfPig](https://github.com/UglyToad/PdfPig) |
| Scanned & amended PDFs | [PaddleSharp](https://github.com/sdcb/PaddleSharp) OCR (PP-OCRv4, MKL-DNN) |
| Storage | SQLite + Entity Framework Core |
| Export | CSV, multi-sheet Excel (ClosedXML) |
| **PDF page preview (UI)** | [PDFtoImage](https://www.nuget.org/packages/PDFtoImage/) / **PDFium** (bundled native DLLs — no separate install) |
| UI | ASP.NET Core Razor Pages + Tailwind (CDN) |

---

## Table of contents

1. [What it does](#what-it-does)
2. [Solution structure](#solution-structure)
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

- **Header metadata** — part number, operation, revision, machine, tool list ID, etc.
- **One row per tool** — tool number, name, holder, diameters, lengths, supplier, path time, remarks, and related columns.
- **PDF characteristics** — digital vs scanned, amendment overlays, template type (A/B/C), OCR confidence, revision conflicts.

Results are browsable in the UI, exportable per file or in bulk, and tied to a stable **file content hash** so re-running extraction on the same file replaces prior rows (re-extract is always enabled).

---

## Solution structure

```
ToolingExtractor/
├── python/table_extractor/   # pdfplumber / camelot / tabula pipeline (see ../python/README.md)
├── src/
│   ├── ToolingExtractor.Core/          # Domain models, enums, options, interfaces
│   ├── ToolingExtractor.Infrastructure/ # EF Core, PDF/OCR, parsers, export
│   ├── ToolingExtractor.Application/   # Pipeline, upload, folder scan, reset
│   └── ToolingExtractor.Web/           # Razor UI, REST API, Program.cs
├── tests/
│   ├── ToolingExtractor.Core.Tests/
│   └── ToolingExtractor.Infrastructure.Tests/
├── models/ppocr_v4/                    # Paddle OCR models (not in git — see setup)
├── scripts/download-models.ps1         # One-time model download helper
└── README.md                           # This file
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

```powershell
cd ToolingExtractor

# 1) Restore, build, (optional) download OCR models — see next section
dotnet restore
dotnet build

# 1b) Python table extractor (strongly recommended for Template A WI PDFs)
cd ..\python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r table_extractor\requirements.txt
cd ..\ToolingExtractor

# 2) Apply database schema (first run also migrates on startup)
dotnet ef database update `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web

# 3) Run the web app
dotnet run --project src/ToolingExtractor.Web
```

Open the URL printed in the console (often `http://localhost:5000` or `http://localhost:5261`).

**Typical first run**

1. Go to **Extract**.
2. Click **Import Files** → multi-select PDFs from any folder (Windows file dialog).
3. Wait for upload confirmation and the file list.
4. Click **Extract Tooling Data** (OCR may take several minutes per large scanned file).
5. Open **Files Processed** → click a **Tool List ID** link → view tools or export CSV/Excel for that file.
6. Use **Export All** for a full-database CSV/Excel download.

---

## OCR model setup

OCR models are **not** downloaded at runtime. You must install them once before scanned/amended PDF extraction works.

### Option A — PowerShell script (recommended)

```powershell
cd ToolingExtractor
.\scripts\download-models.ps1
```

This populates `models/ppocr_v4/` with `det`, `cls`, and `rec` inference folders.

### Option B — Manual layout

Extract PP-OCRv4 English models so each subfolder contains `inference.pdmodel` (or `inference.json`) and `inference.pdiparams`:

```
models/ppocr_v4/
├── det/
├── cls/
└── rec/          # must include en_dict.txt
```

See [models/ppocr_v4/README.md](models/ppocr_v4/README.md) if present.

The app searches several roots (`ToolingPaths`) so models work whether you start from `ToolingExtractor/` or `src/ToolingExtractor.Web/`.

---

## Configuration reference

Edit **`src/ToolingExtractor.Web/appsettings.json`** → section `ToolingExtractor`.

| Setting | Default | Description |
|---------|---------|-------------|
| `AllowedBasePaths` | `C:\Tools\PDFs`, … | Folders allowed for **legacy** folder scan API (`POST /api/folder/import`). Not required for browser Import Files. |
| `UploadStagingPath` | `./data/uploads` | Where uploaded PDFs are stored per import batch. Cleared on data reset. |
| `MaxUploadFileBytes` | `209715200` (200 MB) | Maximum size per uploaded PDF. |
| `PaddleOcrModelPath` | `./models/ppocr_v4` | OCR model root. |
| `PdfRenderDpi` | `300` | Rasterization DPI for OCR path. |
| `MaxPageRenderPixels` | `4000` | Longest edge cap per page (prevents Skia OOM on large sheets). |
| `PerFileTimeoutSeconds` | `600` | Per-PDF processing timeout (raise for very large OCR jobs). |
| `MaxParallelFiles` | `4` | Parallel PDF workers on local disks. |
| `NetworkPathThrottleParallelism` | `2` | Parallelism when folder path is a UNC/network drive. |
| `OcrConfidenceThreshold` | `0.6` | Confidence threshold used in OCR-related logic. |
| `FailedExtractionOutputPath` | `./failed_extractions` | Failed parse dumps and `errors.log`. |
| `AmendedPdfLogPath` | `./amended_pdf_log` | Amendment detection JSON logs. |
| `AmendmentDetection` | — | Whiteout/overlap thresholds for amended PDF detection. |
| `ImagePreprocessing` | — | Brightness/deskew settings before OCR. |
| `PythonTableExtraction:Enabled` | `true` | Run Python pdfplumber/camelot/tabula for Template A. |
| `PythonTableExtraction:PythonExecutable` | `python` | Python on PATH (or full path to venv python). |
| `PythonTableExtraction:TimeoutSeconds` | `180` | Subprocess timeout per PDF. |
| `PythonTableExtraction:MinimumToolRows` | `3` | Minimum valid `T##` rows to accept Python output. |

**Logging** — `ToolingExtractor` namespace defaults to `Debug` in appsettings for detailed pipeline logs during development.

**Upload size (server)** — Kestrel and form options allow multi-file uploads up to **2 GB** total per request (see `Program.cs`). Adjust if you need larger batches.

---

## Using the web UI

### Dashboard (`/`)

Summary counts: total records, digital vs scanned, amended PDFs, revision conflicts. Data loads after any automatic session reset completes (see below).

### Extract (`/Extract`)

| Control | Action |
|---------|--------|
| **Import Files** | Opens the OS file picker. Uploaded PDFs appear in a table (No., Filename, **eye icon**). |
| **Eye icon (preview)** | Loads a **full-page** JPEG in the right panel (text, borders, and graphics via PDFium). Multi-page PDFs: use ‹ › under the filename. |
| **Extract Tooling Data** | Enabled after import. Progress bar, log, and elapsed timer (`HH:MM:SS`). **Always re-extracts** matching file hashes. |
| **Clear All** | Wipes SQLite, upload staging, `failed_extractions`, and `amended_pdf_log`. Starts a fresh session. |

**Import tips**

- Use Ctrl+Click or Shift+Click to multi-select in the file dialog.
- Duplicate filenames in one batch are renamed (`file_2.pdf`, etc.).
- If import seems to do nothing after selecting files, hard-refresh (Ctrl+F5) — fixed in v3.3.1 (picker was cleared before read).

### Files Processed (`/FilesProcessed`)

One row per extracted PDF: Tool List ID (link), part, operation, revision. **Edit** metadata inline; **Delete** removes all tool rows for that file hash. Filter by part/operation. Link to bulk Excel export.

### Tool List Data Extracted (`/ToolListData?hash=…`)

Full tooling table for a single file. **Download CSV** / **Download Excel** export only that file’s rows (`?hash=` on export APIs).

### Export All (`/ExportAll`)

Downloads **all** tooling records in the current database as CSV or Excel. Legacy URL `/Export` redirects here.

---

## Session and data lifecycle

| Event | What gets cleared |
|--------|-------------------|
| **Browser reload (F5)** or **first visit in a new tab** | SQLite DB recreated (migrate), `failed_extractions`, `amended_pdf_log`, `data/uploads` |
| **Clear All** on Extract | Same as above, without leaving the page |
| **Navigate between pages** (same tab, no F5) | Data **kept** until reload or Clear All |

The layout calls `POST /api/data/reset` on reload/new tab. Pages that load API data wait on `waitForDataReset()` so they do not read stale data mid-reset.

**Implication** — Finish extraction and export before pressing F5, or you will lose in-memory session results until you import and extract again.

---

## Python table extraction (Template A)

For **Master Tooling List** PDFs with ruled borders, the app prefers a Python pipeline that preserves column alignment better than line-based OCR text.

### Strategy order

1. **pdfplumber-wordgrid** — reads vertical PDF rules, assigns each word to 1 of 12 columns (best for landscape Master Tooling List / `SAMPLE.pdf`)  
2. **pdfplumber** — `vertical_strategy` / `horizontal_strategy`: `lines`, tuned tolerances for WI tables  
3. **camelot** — `flavor='lattice'` then `stream` (install Ghostscript for lattice)  
4. **tabula-py** — lattice/stream fallback  

### Output columns (pandas → JSON)

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

Each run produces **raw** and **cleaned** row sets. Cleaning includes:

- Footer/stamp row removal (`CAM Programmer`, `Approved by`, …)  
- `T##` validation (`T01`, `T02`, …)  
- Broken decimal repair (`63 . 000` → `63.000`)  
- Multi-line description merge for continuation rows  

### Standalone test

```powershell
cd python
.\.venv\Scripts\Activate.ps1
python -m table_extractor --pdf "C:\Tools\PDFs\TYPE_1_F57551907200 OP10 REV01_WI.pdf"
```

### Disable Python

Set `ToolingExtractor:PythonTableExtraction:Enabled` to `false` in appsettings to use only C# text/OCR parsers.

Full Python docs: [../python/README.md](../python/README.md).

---

## How PDFs are processed

High-level pipeline (background job per **Extract** click):

```
Upload batch (staging folder)
    → For each PDF (parallel, with per-file timeout):
        → SHA-256 hash
        → If hash exists in DB → delete old rows (re-extract always on)
        → Classify PDF (Digital / Scanned / Mixed / Amended)
        → Extract text (PdfPig and/or PaddleOCR on rendered pages)
        → Detect template A, B, or C
        → Template A: Python table extract (pdfplumber/camelot/tabula) OR fallback text parse
        → Templates B/C: Parse tooling header + tool rows from text
        → Optional: amendment flag, revision conflict check
    → Bulk insert ToolingRecord rows
    → Update job status (processed / skipped / failed / amended counts)
```

**Skipped count** — Usually zero when re-extract is on; skipped typically meant dedup in older versions.

**Failed files** — Template not recognized, timeout, or file locked. Check `failed_extractions/<filename>.pdf.txt` for raw text and reason; `errors.log` for exceptions.

**Amended PDFs** — Logged under `amended_pdf_log/`; Excel export includes review-oriented sheets where applicable.

---

## Tooling templates

| Template | Typical documents |
|----------|-------------------|
| **A** | Master Tooling List / WI variants (including landscape single-line tool tables) |
| **B** | Template B tooling list layouts |
| **C** | Template C tooling list layouts |

Detection is automatic from extracted text. Unknown templates count as **failed** for that file.

Extracted tool columns (per row) include: Tool No., Tool Name, Consumable Description, Supplier, Holder, Diameter (D1), Flute Length (L1), Extension Length (L2), Corner Radius, Arbor Description, Tool Path Time, Remarks, plus document-level metadata on each record.

---

## REST API

Base URL: same origin as the web app (e.g. `http://localhost:5261`).

### Extraction

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/files/import` | **Multipart** form field `files` (repeatable). Uploads PDFs; returns `{ folderPath, batchId, count, files[] }`. |
| `GET` | `/api/files/preview/meta` | Query `folderPath`, `relativePath` → `{ fileName, pageCount }`. |
| `GET` | `/api/files/preview` | Query `folderPath`, `relativePath`, optional `page` → full-page JPEG (PDFium). |
| `POST` | `/api/folder/import` | JSON `{ folderPath }`. Lists PDFs under an **AllowedBasePaths** folder (legacy; no upload). |
| `POST` | `/api/extraction/start` | JSON `{ folderPath, filePaths[] }`. `folderPath` from import response; `filePaths` relative names. Always re-extracts. Returns `{ jobId }`. |
| `GET` | `/api/extraction/status/{jobId}` | Job progress and counts. |
| `GET` | `/api/extraction/{jobId}/visualizer` | Live snapshot: file name, stage, page index, normalized highlight rectangles (`x`,`y`,`w`,`h` 0–1). |
| `GET` | `/api/extraction/{jobId}/visualizer/preview?page=N` | JPEG render of the current PDF page for the active file. |

### Files and records

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/files` | Paginated processed-file summaries. Query: `partNumber`, `operation`, `page`, `pageSize`. |
| `GET` | `/api/files/{hash}` | File summary + all tool rows for content hash. |
| `PATCH` | `/api/files/{hash}` | Update displayed metadata (tool list ID, part, op, rev). |
| `DELETE` | `/api/files/{hash}` | Delete all records for hash. |
| `GET` | `/api/records` | Paginated raw records with filters. |

### Export

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/export/csv` | All records, or `?jobId=`, or `?hash=` for one file. |
| `GET` | `/api/export/excel` | Same filters; multi-sheet workbook. |

### Admin / dashboard

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/dashboard` | Aggregate stats for Dashboard page. |
| `POST` | `/api/data/reset` | Delete DB, migrate, clear log/upload folders. |

---

## Output folders

Paths are resolved relative to the solution/web working directory (`ToolingPaths`).

| Path | Purpose |
|------|---------|
| `./data/tooling.db` | SQLite database |
| `./data/uploads/{batchId}/` | PDFs from **Import Files** (temporary per batch) |
| `./failed_extractions/` | `*.pdf.txt` failed parse dumps, `errors.log`, `revision_conflicts.log` |
| `./amended_pdf_log/` | Amendment JSON per file |

All except the database file are **emptied** on reset; the database is dropped and recreated via EF migrations.

---

## Troubleshooting

| Symptom | Things to check |
|---------|------------------|
| Startup: AVX2 error | CPU too old for MKL-DNN; must run on supported hardware. |
| Startup: Paddle DLL missing | `dotnet restore`, rebuild Web project; verify `paddle_inference*.dll` in output folder. |
| Startup: model error | Run `scripts/download-models.ps1`; verify `det`, `cls`, `rec` under `models/ppocr_v4`. |
| Import does nothing after picking files | Hard-refresh (v3.3.1+). Open browser devtools → Console for JS errors. |
| Tool columns jumbled on WI PDF | Install Python deps (`pip install -r python/table_extractor/requirements.txt`). Check logs for `Python table extraction: N rows via pdfplumber`. |
| Python not used | Ensure `python/table_extractor` exists relative to app cwd; set `PythonExecutable` to venv python. |
| Import HTTP 400 | Non-PDF selected, empty files, or file &gt; `MaxUploadFileBytes`. |
| Extract: all failed | Open `failed_extractions/*.txt` — template mismatch or empty OCR. Try digital PDF sample first. |
| Extract: timeout | Increase `PerFileTimeoutSeconds` in appsettings. |
| Extract: file locked | Close PDF in Foxit/Adobe; retry. |
| Files Processed empty but export has rows | Refresh after job completes; re-extract if schema changed (older bug fixed in 3.1.2). |
| MSB3027 / DLL locked on build | Stop running `ToolingExtractor.Web` process, then rebuild. |
| 0 tools on WI landscape sheet | Re-run Extract (re-extract replaces rows); ensure v3.1.1+ parser for inline T01–Tnn rows. |

**Logs** — Watch the console where `dotnet run` is active; `ToolingExtractor` logs at Debug show per-file decisions.

---

## Development

```powershell
# Run all tests
dotnet test

# EF migrations (after model changes)
dotnet ef migrations add <Name> `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web

dotnet ef database update `
  --project src/ToolingExtractor.Infrastructure `
  --startup-project src/ToolingExtractor.Web
```

**Sample PDF testing** — Infrastructure tests may reference WI samples under configured test paths; see `tests/ToolingExtractor.Infrastructure.Tests/`.

**Version** — Shown in the sidebar (`_Layout.cshtml`). Release notes: [../CHANGELOG.md](../CHANGELOG.md).

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
