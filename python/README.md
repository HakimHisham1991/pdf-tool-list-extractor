# Python table extractor

Border-aware tooling table extraction for aerospace **Master Tooling List** PDFs.

## Strategy (in order)

1. **pdfplumber** — `vertical_strategy` / `horizontal_strategy`: `lines` (best for ruled tables)
2. **camelot** — `flavor='lattice'` then `stream` (needs [Ghostscript](https://www.ghostscript.com/download/gsdnld.html) on PATH)
3. **tabula-py** — lattice then stream fallback

Outputs **raw** and **cleaned** pandas-compatible JSON with columns:

`Tool_No`, `Tool_Name`, `Consumable_Tool_Description`, `Tool_Supplier`, `Tool_Identifier`, `Total_Diameter`, `Flute_Length`, `Total_Length`, `Total_Corner_Radius`, `Anchor_Description`, `Tool_Path_Time_In_Minutes`, `Remarks`

## Setup

```powershell
cd python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r table_extractor/requirements.txt
```

Optional for camelot lattice: install Ghostscript and ensure `gswin64c` is on PATH.

## Run standalone

```powershell
python -m table_extractor.cli --pdf "C:\Tools\PDFs\TYPE_1_F57551907200 OP10 REV01_WI.pdf"
```

Exit code `0` = success, `2` = no table extracted.

## .NET integration

ToolingExtractor calls this module automatically for **Template A** when `ToolingExtractor:PythonTableExtraction:Enabled` is true and `python` is available (see [../README.md](../README.md)).
