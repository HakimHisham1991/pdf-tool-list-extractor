"""Camelot lattice extraction (bordered tables) — requires Ghostscript on PATH."""

from __future__ import annotations

import re
from typing import Any

from .columns import is_tool_header_row, map_headers
from .pdfplumber_extract import _merge_header_rows, _score_table


def extract_with_camelot(pdf_path: str) -> tuple[list[list[str | None]] | None, list[list[str | None]] | None, str]:
    try:
        import camelot
    except ImportError as ex:
        return None, None, f"camelot: not installed ({ex})"

    best_table: list[list[Any | None]] | None = None
    best_score = 0
    last_err = ""
    winning_flavor = "lattice"

    for flavor in ("lattice", "stream"):
        try:
            tables = camelot.read_pdf(
                pdf_path,
                pages="all",
                flavor=flavor,
                line_scale=40,
                strip_text="\n",
            )
        except Exception as ex:
            last_err = str(ex)
            continue

        for table in tables:
            data = table.df.values.tolist()
            sc = _score_table(data)
            if sc > best_score:
                best_score = sc
                best_table = data
                winning_flavor = flavor

    if not best_table or best_score < 20:
        return None, None, f"camelot: no table ({last_err})"

    rows = [[str(c).strip() if c is not None else None for c in row] for row in best_table]
    header_idx = next(
        (i for i, row in enumerate(rows[:5]) if is_tool_header_row([str(c or "") for c in row])),
        0,
    )
    header_cells, body = _merge_header_rows(rows, header_idx)
    return [header_cells], body, f"camelot:{winning_flavor}"
