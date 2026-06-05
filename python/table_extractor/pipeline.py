"""Orchestrate pdfplumber → camelot → tabula for Master Tooling List tables."""

from __future__ import annotations

from typing import Any

import pandas as pd

from .camelot_extract import extract_with_camelot
from .clean import clean_dataframe, rows_to_dataframe
from .columns import CANONICAL_COLUMNS, map_headers
from .highlights import extract_page_highlights
from .pdfplumber_extract import extract_with_pdfplumber
from .pdfplumber_wordgrid import extract_with_wordgrid
from .tabula_extract import extract_with_tabula


def _build_frames(
    header_rows: list[list[str | None]] | None,
    body_rows: list[list[str | None]] | None,
    method: str,
) -> dict[str, Any] | None:
    if not header_rows or not body_rows:
        return None

    column_map = map_headers(header_rows[0])
    if sum(1 for c in column_map if c) < 8:
        # Positional fallback — wordgrid supplies 12 aligned columns.
        column_map = list(CANONICAL_COLUMNS[: min(len(body_rows[0]) if body_rows else 0, len(CANONICAL_COLUMNS))])
        while len(column_map) < len(CANONICAL_COLUMNS) and len(column_map) < (len(body_rows[0]) if body_rows else 0):
            column_map.append(CANONICAL_COLUMNS[len(column_map)])

    raw_df = rows_to_dataframe(body_rows, column_map)
    raw_copy, cleaned_df, validation = clean_dataframe(raw_df)

    if validation.get("row_count_cleaned", 0) < 1:
        return None

    return {
        "method": method,
        "columns": CANONICAL_COLUMNS,
        "raw": raw_copy.to_dict(orient="records"),
        "cleaned": cleaned_df.to_dict(orient="records"),
        "validation": validation,
    }


def _attach_highlights(result: dict[str, Any], pdf_path: str) -> dict[str, Any]:
    try:
        result["highlights"] = extract_page_highlights(pdf_path)
    except Exception:
        result["highlights"] = []
    return result


def extract_tooling_table(pdf_path: str) -> dict[str, Any]:
    errors: list[str] = []
    strategy_order = ["pdfplumber-wordgrid", "pdfplumber", "camelot", "tabula"]

    header, body, method = extract_with_wordgrid(pdf_path)
    result = _build_frames(header, body, method)
    if result:
        result["strategy_order"] = strategy_order
        return _attach_highlights(result, pdf_path)
    errors.append(method)

    header, body, method = extract_with_pdfplumber(pdf_path)
    result = _build_frames(header, body, method)
    if result:
        result["strategy_order"] = strategy_order
        return _attach_highlights(result, pdf_path)
    errors.append(method)

    header, body, method = extract_with_camelot(pdf_path)
    result = _build_frames(header, body, method)
    if result:
        result["strategy_order"] = strategy_order
        return _attach_highlights(result, pdf_path)
    errors.append(method)

    header, body, method = extract_with_tabula(pdf_path)
    result = _build_frames(header, body, method)
    if result:
        result["strategy_order"] = strategy_order
        return _attach_highlights(result, pdf_path)
    errors.append(method)

    return {
        "success": False,
        "errors": errors,
        "columns": CANONICAL_COLUMNS,
        "raw": [],
        "cleaned": [],
        "validation": {"row_count_cleaned": 0, "tool_numbers": []},
    }
