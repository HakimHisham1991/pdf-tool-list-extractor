"""pdfplumber-based bordered table extraction."""

from __future__ import annotations

import re
from typing import Any

import pdfplumber

from .columns import is_tool_header_row, map_headers

# Tuned for aerospace WI / Master Tooling List lined tables.
TABLE_SETTINGS: dict[str, Any] = {
    "vertical_strategy": "lines",
    "horizontal_strategy": "lines",
    "intersection_tolerance": 8,
    "snap_tolerance": 4,
    "join_tolerance": 4,
    "edge_min_length": 8,
    "min_words_vertical": 1,
    "min_words_horizontal": 1,
}


def _score_table(table: list[list[Any | None]]) -> int:
    if not table or len(table) < 2:
        return 0
    score = 0
    header_idx = -1
    for i, row in enumerate(table[:4]):
        cells = [str(c or "") for c in row]
        if is_tool_header_row(cells):
            header_idx = i
            score += 50
            break
    if header_idx < 0:
        return 0
    for row in table[header_idx + 1 :]:
        joined = " ".join(str(c or "") for c in row)
        if re.search(r"\bT\d{2}\b", joined, re.IGNORECASE):
            score += 10
    score += min(len(table), 30)
    return score


def _merge_header_rows(rows: list[list[str | None]], header_idx: int) -> tuple[list[str | None], list[list[str | None]]]:
    """Combine multi-line header rows (merged cells)."""
    header = [str(c or "").strip() for c in rows[header_idx]]
    body_start = header_idx + 1
    if body_start < len(rows):
        next_row = [str(c or "").strip() for c in rows[body_start]]
        if is_tool_header_row(next_row) or (
            sum(1 for c in next_row if c) >= 3
            and not re.search(r"\bT\d{2}\b", " ".join(next_row), re.IGNORECASE)
        ):
            merged = []
            width = max(len(header), len(next_row))
            for i in range(width):
                a = header[i] if i < len(header) else ""
                b = next_row[i] if i < len(next_row) else ""
                merged.append(f"{a} {b}".strip() if a and b else (a or b))
            header = merged
            body_start += 1
    body = [[str(c or "").strip() or None for c in row] for row in rows[body_start:]]
    return header, body


def extract_with_pdfplumber(pdf_path: str) -> tuple[list[list[str | None]] | None, list[list[str | None]] | None, str]:
    best_table: list[list[Any | None]] | None = None
    best_score = 0

    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            try:
                tables = page.extract_tables(table_settings=TABLE_SETTINGS)
            except Exception:
                tables = []
            if not tables:
                try:
                    tables = page.extract_tables()
                except Exception:
                    tables = []
            for table in tables or []:
                sc = _score_table(table)
                if sc > best_score:
                    best_score = sc
                    best_table = table

    if not best_table or best_score < 20:
        return None, None, "pdfplumber: no tooling table detected"

    rows = [[str(c).strip() if c is not None else None for c in row] for row in best_table]
    header_idx = next(
        (i for i, row in enumerate(rows[:5]) if is_tool_header_row([str(c or "") for c in row])),
        0,
    )
    header_cells, body = _merge_header_rows(rows, header_idx)
    column_map = map_headers(header_cells)
    return [header_cells], body, "pdfplumber"
