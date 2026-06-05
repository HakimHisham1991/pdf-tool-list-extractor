# Changelog

## [3.9.9] - 2026-06-05

### Fixed

- **Diameter (Ø) and degree (°) symbols** — Python subprocess output is read as UTF-8; tooling text fields are normalised to preserve PDF symbols and repair common mojibake/replacement-character corruption (e.g. `Chamfer Ø8 x 60°`).

## [3.9.8] - 2026-06-05

### Added

- **Clear Files** on Files Processed — deletes all processed PDF tooling data (and saved highlight overlays) in one action.

### Changed

- **ToolListId** — now stores and displays the original PDF filename (e.g. `351-2223-4_OP20_REV04_Tooling (SECO STAMPED).pdf`) instead of parsed PDF header text.

## [3.9.7] - 2026-06-05

### Fixed

- **Remarks column** — Python table extraction no longer overwrites blank Remarks with `[TABLE:…]` metadata; the field now mirrors the source PDF (empty when the PDF cell is empty).

## [3.9.6] - 2026-06-05

### Fixed

- **SECO stamped tooling lists** — PDFs that use numeric tool numbers (`10`, `11`, …) instead of `T01`/`T02` are now parsed correctly. Python validation (`TOOL_NO_PATTERN`), .NET `ParserA` space-delimited rows, and `DigitalPdfExtractor` highlights all accept both formats.

## [3.9.5] - 2026-06-05

### Fixed

- **Build/package warnings** — Pinned `SharpCompress` 0.48.0 and `System.Security.Cryptography.Xml` 10.0.6 to resolve NuGet vulnerability advisories (GHSA-6c8g-7p36-r338, GHSA-37gx-xxp4-5rgx, GHSA-w3x6-4m5h-cxqf).
- **CA1416 platform warnings** — PDFium and Windows identity calls are guarded with `[SupportedOSPlatform("windows")]` (app targets Windows for Paddle/PDFium).
- **Paddle stderr noise** — Set `GLOG_minloglevel=2` before native init to suppress deprecated oneDNN API warnings from the bundled Paddle runtime.

## [3.9.4] - 2026-06-05

### Fixed

- **Clear All resets elapsed timer** — Elapsed clock returns to `00:00:00` and is hidden when Clear All is clicked.

## [3.9.3] - 2026-06-05

### Fixed

- **Clear All after Stop** — Clear All is re-enabled immediately when Stop is clicked, without waiting for the job to finish cancelling.

## [3.9.2] - 2026-06-05

### Fixed

- **Stop button** — Always uses primary blue styling and stays clickable; no longer greyed out when idle.
- **Clear All button** — Primary blue when OCR/extraction is not running; disabled (greyed) only while a job is active.

## [3.9.1] - 2026-06-05

### Fixed

- **Per-file highlight layers** — OCR/table/extracted overlays are now stored separately for each PDF (in-memory and SQLite). Clicking the eye icon loads highlights for that file only, not the last processed file.

## [3.9.0] - 2026-06-05

### Added

- **Stop** button on Extract page — cancels the running extraction job after the current file finishes; preview image and OCR overlays stay on screen.
- **Clear All** button — clears the imported file list, log, progress, and PDF preview (does not wipe processed tooling records in the database).
- **Persistent OCR highlight overlays** — normalized highlight rectangles (type, coordinates, colors) are saved to SQLite per file/page after each PDF is processed. Clicking the eye icon reloads overlays from `GET /api/files/highlights`.
- `POST /api/extraction/{jobId}/stop` — request cancellation of a background extraction job.
- `JobStatus.Cancelled` for stopped jobs.

## [3.8.1] - 2026-06-05

### Fixed

- **Extract page — Import Files always active** — Import Files no longer uses grey secondary styling or gets disabled during upload/extraction; only Extract Tooling Data is disabled while a job runs.

## [3.8.0] - 2026-06-05

### Changed

- **UI color theme** — Replaced the dark cyan/navy palette with a light theme aligned to the PDC Database palette: off-white background (`#FBFBFB`), primary blue (`#2453B3`), muted blue-grey secondary (`#9BA9B8`), and neutral greys for text and borders. Sidebar, buttons, cards, tables, progress bar, and log styling updated via shared CSS variables in `app.css`.

## [3.7.5] - 2026-06-05

### Fixed

- **Critical: DB wiped on navigation** — `sendBeacon`/API calls without session cookies triggered middleware that reset the database. API routes no longer auto-reset; only HTML page loads (new tab) or F5 reload reset data.
- Extract workspace restores from **sessionStorage first**, then server file fallback.
- Tab id header (`X-Te-Tab-Id`) keys workspace when saving during page leave without cookies.
- Sidebar navigation from Extract saves workspace before leaving the page.

## [3.7.4] - 2026-06-05

### Fixed

- **Session data lost on navigation** — Root cause was an async `/api/session/ensure` race: navigating before the session cookie was set triggered a full DB reset on the next page.
- Session cookie is now set synchronously via **middleware** on the first HTML/API request (no reset on later navigation).
- Extract workspace persisted to **disk** (`data/session-workspaces/`) instead of in-memory only.
- Workspace save on page leave uses `sendBeacon` for reliable delivery when clicking away.

### Changed

- Reset on F5 only via `POST /api/session/reload`; removed per-page `/api/session/ensure` reset call.

## [3.7.3] - 2026-06-05

### Fixed

- **Session persistence (reliable)** — Browser session is now tracked with an HttpOnly cookie on the server. DB and Extract workspace are **not** reset when navigating between Extract, Files Processed, and Tool List Data.
- Reset runs only when the session cookie is missing (new tab/window after close) or on F5 reload.
- Extract workspace saved to server (`PUT /api/session/workspace`) in addition to `sessionStorage`.

## [3.7.2] - 2026-06-05

### Fixed

- **Session persistence**: Import list, extraction log, preview state, and DB rows now survive navigation between Extract, Files Processed, and Tool List Data in the same tab.
- Extract workspace is saved to `sessionStorage` and restored when returning to the Extract page.

### Changed

- Data reset runs only on **browser reload**, **new tab/window**, or **after closing the tab** (next visit). Navigation between app pages no longer clears data.
- Removed **Clear All** button (reset is automatic on reload/close only).

## [3.7.1] - 2026-06-05

### Fixed

- **PDF preview flicker** during extraction: visualizer polling no longer reloads the JPEG on every tick; image refreshes only when the page changes, and highlight overlays update when snapshot data changes.
- Visualizer polling stops when extraction completes (was continuing indefinitely).

### Changed

- PDF preview panel moved below the import file table and spans the same full width as the table (single-column layout).

## [3.7.0] - 2026-06-05

### Added

- **Multi-layer PDF highlight overlays** on the Extract preview during and after extraction: OCR text (blue), table borders (gray), extracted tool rows (green), low-confidence OCR (red), and ignored header/footer bands (yellow).
- Canvas overlay with independent layer toggles and a **Reset View** button.
- API: `GET /api/extraction/{jobId}/visualizer/highlights?page=N` returns normalized highlight boxes per page.
- Python table pipeline exports page highlight metadata (pdfplumber edges, table bounds, ignored bands).

### Changed

- Live extraction visualizer polling restored on the Extract page with multi-layer rendering.
- Highlight data accumulates per page in the visualizer store until the job completes (no longer cleared at pipeline end).

## [3.6.1] - 2026-06-04

### Fixed

- **PDF preview** now uses **PDFium** (NuGet `PDFtoImage` / `bblanchon.PDFium`) so pages render with vector text, table borders, and images — not embedded bitmaps only.
- Tiled rendering enabled for large WI/tool-list pages that previously broke PdfPig/Skia.

### Changed

- SkiaSharp bumped to 3.119.2 (PDFtoImage dependency).

## [3.6.0] - 2026-06-04

### Added

- **Import file table** on Extract: columns No., Filename, and PDF preview (eye icon).
- **Static PDF preview panel**: click the eye icon to load a full-page preview before extraction (`GET /api/files/preview`, `GET /api/files/preview/meta`).
- Multi-page preview navigation (‹ ›) when a PDF has more than one page.

### Changed

- Removed live extraction visualizer polling from the Extract UI (log + progress only during extract).
- Preview rendering composites embedded page images when full-page Skia render fails (fixes garbled thumbnails on WI/tool-list PDFs).

## [3.5.1] - 2026-06-04

### Fixed

- **Live PDF preview** during extraction: WI/tool-list PDFs with very large embedded scans no longer show a blank thumbnail. Preview falls back to the largest embedded page image when full-page Skia render cannot allocate a bitmap; stage metadata (page count, highlights) is preserved across Python table extraction.

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
