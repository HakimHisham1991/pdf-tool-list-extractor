# Changelog

## [3.5.0] - 2026-06-04

### Added

- **Extraction elapsed timer** on the Extract page (`HH:MM:SS`) while a job is running.
- **Live PDF visualizer** during extraction: page preview, stage messages, and highlighted OCR/digital regions (FineReader-style active-region cycling in the UI).
- API: `GET /api/extraction/{jobId}/visualizer` and `GET /api/extraction/{jobId}/visualizer/preview`.

## [3.4.2] - 2026-06-04

### Added

- **pdfplumber-wordgrid** extractor: uses PDF vertical rules + word positions for 12-column Master Tooling List tables (fixes jumbled OCR columns on landscape WI PDFs like `SAMPLE.pdf`).

### Fixed

- Python `is_footer_row` import error in `clean.py`.
- C# inline WI parser stops at footer blocks (no stamp/approval text in last tool row).
- Auto-detect `python/.venv/Scripts/python.exe` when `PythonExecutable` is default `python`.

## [3.4.1] - 2026-06-04

### Fixed

- App startup crash: register `ParserA` in DI for `TemplateAParseService` (was only registered as `IToolingParser`).

## [3.4.0] - 2026-06-04

### Added

- **Python table extraction** for Template A (Master Tooling List): pdfplumber → camelot → tabula pipeline with raw + cleaned DataFrame columns (`Tool_No`, `Tool_Name`, … `Remarks`).
- `python/table_extractor/` package and .NET bridge (`PythonTableExtractionBridge`, `TemplateAParseService`).
- Config: `ToolingExtractor:PythonTableExtraction` in appsettings.json.

### Changed

- Template A extraction tries Python bordered-table parsing first; falls back to PdfPig/OCR text parsing if Python is unavailable or returns too few rows.
- Parser column aliases extended for Tool Identifier, Total Diameter/Length, Anchor Description.

## [3.3.1] - 2026-06-04

### Changed

- Expanded **ToolingExtractor/README.md** and root **README.md** (setup, UI, API, config, troubleshooting).

### Fixed

- Import Files did nothing after selecting PDFs — file list was cleared before upload (silent no-op).

## [3.3.0] - 2026-06-04

### Added

- **Import Files** opens the OS file browser with multi-select PDF; files upload from any folder (no `AllowedBasePaths` typing required).
- `POST /api/files/import` (multipart) and `./data/uploads` staging area.

### Changed

- Extract page no longer uses a folder path text box for import.

## [3.2.1] - 2026-06-04

### Added

- **Clear All** on Extract page — wipes database and log folders for a new session.

### Changed

- Nav **Export** renamed to **Export All** (`/ExportAll`; `/Export` redirects).
- Re-extract is always on when extracting (checkbox removed).
- Extract completion banner links to Export All.

## [3.2.0] - 2026-06-04

### Added

- **Extract** page: **Import Files** lists PDFs in the folder without processing; **Extract Tooling Data** runs only on the imported list.
- **Tool List Data Extracted** page: per-file **Download CSV** and **Download Excel** (`?hash=` on export APIs).
- Browser reload (F5) or a new tab session clears SQLite data and wipes `failed_extractions` / `amended_pdf_log` (in-app navigation keeps data until reload).

### Changed

- `POST /api/extraction/start` requires `filePaths` from import; new `POST /api/folder/import` endpoint.

## [3.1.2] - 2026-06-04

### Fixed

- **Files Processed** list empty while Export had data — file grouping now runs in memory (SQLite-safe).

## [3.1.1] - 2026-06-04

### Fixed

- WI / landscape Master Tooling List PDFs: parse tools when the whole table is one line (T01–T44); fixes empty Tool List Data page and misleading tool count.

## [3.1.0] - 2026-06-04

### Added

- **Files Processed** page: one row per PDF (ToolListId, Part, Op, Rev, Edit/Delete).
- **Tool List Data Extracted** page: full tooling table per file (click blue ToolListId link).
- APIs: `GET/PATCH/DELETE /api/files`, `GET /api/files/{hash}` for file summary and tool rows.

### Changed

- Former Results page redirects to Files Processed.

## [3.0.7] - 2026-06-04

### Fixed

- Extract UI explains when all files are **skipped (dedup)** and links to Results.
- **Re-extract** option removes existing records for the same file hash and runs extraction again.

## [3.0.6] - 2026-06-04

### Fixed

- Amendment detection no longer treats different table cells at nearby coordinates as “overlapping” amendments (fixes false positives on landscape WI/tool lists).
- PDFs with embedded text + amendment markers use **digital** extraction (`Mixed`) instead of OCR-only, which returned empty text on your sample WI.
- OCR path honors PDF page rotation metadata for landscape pages.

## [3.0.5] - 2026-06-04

### Fixed

- Per-file OCR timeout raised to 10 minutes (was 30s); timeouts are logged separately from template failures.
- Cap PDF raster size (`MaxPageRenderPixels`) to avoid Skia “Unable to allocate pixels” on large sheets.
- Failed extractions write reason + raw OCR text under `failed_extractions/`; UI hints when files fail.

## [3.0.4] - 2026-06-04

### Fixed

- Paddle native DLLs are copied to the app output folder and `runtimes/win-x64/native` is added to PATH at startup (fixes `paddle_inference_c` / `mkldnn.dll` load failures when DLLs only existed under `runtimes/`).

## [3.0.3] - 2026-06-04

### Fixed

- Added `Sdcb.PaddleInference.runtime.win64.mkl` native package (fixes `paddle_inference_c` / `mkldnn.dll` not found on extract).
- Aligned PaddleSharp packages to 3.3.1; startup verifies OCR runtime loads before accepting requests.

## [3.0.2] - 2026-06-04

### Fixed

- Extraction now runs in the background; the UI returns immediately with live progress polling (fixes “nothing happens” on Extract).
- Extract page shows errors, busy state, and status updates; default path `C:\Tools\PDFs`.

## [3.0.1] - 2026-06-04

### Fixed

- Resolve `models/ppocr_v4` and `data/` paths when running from `src/ToolingExtractor.Web` (walks up to solution root).
- Added `scripts/download-models.ps1` with working PaddleOCR English model URLs.
- Clearer startup error showing full expected model root path.

## [3.0.0] - 2026-06-04

### Added

- Full **ToolingExtractor** .NET 10 solution: Core, Infrastructure, Application, Web, and xUnit test projects.
- Amendment detection, PDF classification (digital/scanned/amended), PaddleSharp OCR pipeline with local bundled models.
- Image pre-processing (grayscale, Otsu binarization, deskew), page orientation detection, OCR table column sorting.
- Template parsers A/B/C with dynamic header remapping, continuation rows, and `RawExtractedJson` for re-parsing.
- SHA-256 file deduplication, revision conflict detection, QMS audit fields on extraction jobs.
- SQLite + EF Core 10, CSV/Excel export (Review Required sheet), Razor Pages UI with industrial theme.
- Startup validation (AVX2, model files, allowed paths, database).

### Notes

- PDF rendering uses **PdfPig.Rendering.Skia** (Sdcb.PdfiumRenderer is not published on NuGet).
- PaddleOCR packages resolve to **3.0.x** on NuGet; models must be placed offline under `models/ppocr_v4/`.
