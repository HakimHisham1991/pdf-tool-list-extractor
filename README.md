# PDF Tool List Extractor

Monorepo for **ToolingExtractor** — a .NET 10 web application that extracts aerospace CNC tooling tables from PDF work instructions and tooling lists, stores them in SQLite, and provides CSV/Excel export.

**Current version: v3.3.1**

---

## Documentation

| Document | Contents |
|----------|----------|
| **[ToolingExtractor/README.md](ToolingExtractor/README.md)** | **Full guide** — prerequisites, model setup, configuration, UI walkthrough, API reference, troubleshooting |
| **[CHANGELOG.md](CHANGELOG.md)** | Version history and release notes |

---

## Quick start

```powershell
cd ToolingExtractor
dotnet restore
dotnet build

# OCR models (required for scanned PDFs) — one time
.\scripts\download-models.ps1

dotnet run --project src/ToolingExtractor.Web
```

Open the URL shown in the terminal (e.g. `http://localhost:5261`).

**Minimal workflow**

1. **Extract** → **Import Files** (multi-select PDFs from any folder) → **Extract Tooling Data**
2. **Files Processed** → open a file → view tools or export per file
3. **Export All** → download everything as CSV/Excel

---

## Repository layout

```
pdf-tool-list-extractor/
├── ToolingExtractor/          # Main .NET solution (see ToolingExtractor/README.md)
│   ├── src/                   # Core, Infrastructure, Application, Web
│   ├── tests/
│   ├── models/ppocr_v4/       # Paddle OCR models (local, not always in git)
│   └── scripts/download-models.ps1
├── CHANGELOG.md
└── README.md                  # This file
```

---

## Requirements (summary)

- Windows x64, .NET 10 SDK, CPU with **AVX2**
- Paddle OCR models under `ToolingExtractor/models/ppocr_v4/` for scanned/amended PDFs
- See [ToolingExtractor/README.md](ToolingExtractor/README.md) for full prerequisites, `appsettings.json` options, and troubleshooting

---

## License

Third-party components used by ToolingExtractor are listed in [ToolingExtractor/README.md § Third-party licenses](ToolingExtractor/README.md#third-party-licenses).
