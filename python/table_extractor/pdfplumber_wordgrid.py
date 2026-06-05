"""Extract tooling tables by assigning words to columns using PDF vertical rules."""

from __future__ import annotations

import re
from typing import Any

import pdfplumber

from .columns import FOOTER_MARKERS, is_footer_row

# Master Tooling List rows use T01… or numeric tool numbers (e.g. 10, 11) on SECO-stamped PDFs.
TOOL_ROW_RE = re.compile(r"^(?:T(\d{2,3})|(\d{2}))\b", re.IGNORECASE)
TOOL_NO_CELL_RE = re.compile(r"^(?:T\d{2,3}|\d{2})\b", re.IGNORECASE)
DIMENSION_RE = re.compile(r"\d+\.\d{3}")
NUMERIC_CELL_RE = re.compile(r"^\d+\.\d{3}$|^--$")


def _cluster_vline_bounds(page: pdfplumber.page.Page, min_height: float = 25.0) -> list[float]:
    xs: list[float] = []
    for edge in page.edges:
        w = abs(edge.get("width", 0))
        h = edge.get("height", 0)
        if w < 3 and h >= min_height:
            xs.append(float(edge["x0"]))
    if not xs:
        return [17, 52, 167, 314, 365, 474, 498, 522, 554, 584, 683, 734, 818]

    xs = sorted(xs)
    merged: list[float] = []
    for x in xs:
        if not merged or x - merged[-1] > 8:
            merged.append(x)
    if merged[0] > 25:
        merged.insert(0, 17.0)
    merged.append(float(page.width) - 5)
    return merged


def _col_index(x: float, bounds: list[float]) -> int:
    for i in range(len(bounds) - 1):
        if x < bounds[i + 1]:
            return min(i, 11)
    return 11


def _line_text(words: list[dict[str, Any]]) -> str:
    return " ".join(w["text"] for w in sorted(words, key=lambda w: w["x0"]))


def _is_footer_line(text: str) -> bool:
    low = text.lower()
    return any(m in low for m in FOOTER_MARKERS)


def _assign_words_to_cols(words: list[dict[str, Any]], bounds: list[float]) -> list[str]:
    cols = [""] * 12
    for w in sorted(words, key=lambda w: w["x0"]):
        idx = _col_index(w["x0"], bounds)
        cols[idx] = f"{cols[idx]} {w['text']}".strip() if cols[idx] else w["text"]
    return cols


def _merge_prefix_lines(cols: list[str], prefix_words: list[dict[str, Any]], bounds: list[float]) -> list[str]:
    if not prefix_words:
        return cols
    prefix_cols = _assign_words_to_cols(prefix_words, bounds)
    for i in (1, 2):
        if prefix_cols[i]:
            cols[i] = f"{prefix_cols[i]} {cols[i]}".strip() if cols[i] else prefix_cols[i]
    return cols


def extract_with_wordgrid(pdf_path: str) -> tuple[list[list[str | None]] | None, list[list[str | None]] | None, str]:
    with pdfplumber.open(pdf_path) as pdf:
        page = pdf.pages[0]
        bounds = _cluster_vline_bounds(page)
        words = page.extract_words(x_tolerance=2, y_tolerance=3, keep_blank_chars=False)
        if not words:
            return None, None, "wordgrid: no words"

        lines: dict[float, list[dict[str, Any]]] = {}
        for w in words:
            y = round(float(w["top"]), 1)
            lines.setdefault(y, []).append(w)

        sorted_ys = sorted(lines.keys())
        header_y = next(
            (y for y in sorted_ys if "tool no" in _line_text(lines[y]).lower()),
            None,
        )
        if header_y is None:
            return None, None, "wordgrid: Tool No. header not found"

        data_ys = [y for y in sorted_ys if y >= header_y]
        body_rows: list[list[str | None]] = []
        prefix_words: list[dict[str, Any]] = []
        started = False
        skip_until = header_y + 25

        for y in data_ys:
            line_words = lines[y]
            text = _line_text(line_words)

            if _is_footer_line(text):
                break

            if "tool no" in text.lower() and "tool name" in text.lower():
                skip_until = y + 20
                continue

            if y < skip_until and not TOOL_ROW_RE.match(text):
                if not any(k in text.lower() for k in ("(d1)", "(l1)", "(l2)", "diameter", "flute", "corner", "arbor", "path time")):
                    prefix_words.extend(line_words)
                continue

            if TOOL_ROW_RE.match(text) and DIMENSION_RE.search(text):
                started = True
                cols = _assign_words_to_cols(line_words, bounds)
                cols = _merge_prefix_lines(cols, prefix_words, bounds)
                prefix_words = []
                if TOOL_NO_CELL_RE.search(cols[0] or ""):
                    body_rows.append([c or None for c in cols])
                continue

            if not started:
                continue

            if TOOL_NO_CELL_RE.search(text):
                continue

            if any(_col_index(w["x0"], bounds) <= 2 for w in line_words):
                prefix_words.extend(line_words)

        if len(body_rows) < 1:
            return None, None, f"wordgrid: only {len(body_rows)} tool rows"

        header_cells = [
            "Tool No.",
            "Tool Name",
            "Consumable Tool Description",
            "Tool Supplier",
            "Tool Holder",
            "Tool Diameter (D1)",
            "Flute Length (L1)",
            "Tool Ext. Length (L2)",
            "Tool Corner Radius",
            "Arbor Description (or equivalent specs)",
            "Tool Path Time in Minutes",
            "Remarks",
        ]
        return [header_cells], body_rows, "pdfplumber-wordgrid"
