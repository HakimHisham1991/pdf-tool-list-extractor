#!/usr/bin/env python3
"""CLI: extract tooling table from PDF → JSON on stdout."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .pipeline import extract_tooling_table


def main() -> int:
    parser = argparse.ArgumentParser(description="Extract aerospace tooling table from PDF")
    parser.add_argument("--pdf", required=True, help="Path to PDF file")
    parser.add_argument("--output", default="-", help="Output JSON path or '-' for stdout")
    args = parser.parse_args()

    pdf_path = Path(args.pdf)
    if not pdf_path.is_file():
        print(json.dumps({"success": False, "error": f"File not found: {pdf_path}"}), file=sys.stderr)
        return 1

    result = extract_tooling_table(str(pdf_path.resolve()))
    result["success"] = bool(result.get("cleaned")) and result.get("validation", {}).get("row_count_cleaned", 0) > 0
    payload = json.dumps(result, ensure_ascii=False, indent=2)

    if args.output == "-":
        print(payload)
    else:
        Path(args.output).write_text(payload, encoding="utf-8")

    return 0 if result["success"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
