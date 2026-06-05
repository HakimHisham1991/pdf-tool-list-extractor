"""Post-processing and validation for extracted tooling tables."""

from __future__ import annotations

import re
from typing import Any

import pandas as pd

from .columns import CANONICAL_COLUMNS, FOOTER_MARKERS, TOOL_NO_PATTERN, is_footer_row

_SPACE_RE = re.compile(r"\s+")
_BROKEN_DECIMAL_RE = re.compile(r"(\d)\s+[\.,]\s+(\d)")
_MULTI_DOT_RE = re.compile(r"\.{2,}")
_DIAMETER_ALTS = str.maketrans(
    {
        "\u2205": "\u00d8",
        "\u2300": "\u00d8",
        "\u03a6": "\u00d8",
        "\u03c6": "\u00d8",
        "\u00f8": "\u00d8",
    }
)
_MOJIBAKE_REPLACEMENTS = (
    ("Ã˜", "Ø"),
    ("Ã¸", "ø"),
    ("Â°", "°"),
)


def _normalize_engineering_symbols(text: str) -> str:
    for bad, good in _MOJIBAKE_REPLACEMENTS:
        text = text.replace(bad, good)
    text = text.translate(_DIAMETER_ALTS)
    text = re.sub(r"(\d)º", r"\1°", text)
    text = re.sub(r"\ufffd(?=\d)", "Ø", text)
    text = re.sub(r"(?<=\d)\ufffd(?=\s|$|[^\d])", "°", text)
    text = re.sub(r"(\d)x\ufffd", r"\1x°", text)
    return text


def _clean_cell(value: Any) -> str:
    if value is None or (isinstance(value, float) and pd.isna(value)):
        return ""
    text = str(value).replace("\n", " ").replace("\r", " ")
    text = _SPACE_RE.sub(" ", text).strip()
    text = _BROKEN_DECIMAL_RE.sub(r"\1.\2", text)
    text = text.replace(",", ".") if re.search(r"\d,\d{2}\b", text) else text
    return _normalize_engineering_symbols(text)


def normalize_tool_no(value: str) -> str:
    text = _clean_cell(value).upper()
    m = re.search(r"\bT\d{2}\b", text)
    if m:
        return m.group(0).upper()
    m = re.match(r"^(\d{2})$", text)
    return m.group(1) if m else text


def is_valid_tool_row(tool_no: str) -> bool:
    return bool(TOOL_NO_PATTERN.match(tool_no))


def rows_to_dataframe(rows: list[list[str | None]], column_map: list[str | None]) -> pd.DataFrame:
    """Build a DataFrame using canonical columns from body rows."""
    data: dict[str, list[str]] = {col: [] for col in CANONICAL_COLUMNS}
    col_indices: dict[str, int] = {}
    for idx, name in enumerate(column_map):
        if name and name not in col_indices:
            col_indices[name] = idx

    for row in rows:
        if is_footer_row(row):
            break
        for col in CANONICAL_COLUMNS:
            idx = col_indices.get(col)
            val = _clean_cell(row[idx]) if idx is not None and idx < len(row) else ""
            data[col].append(val)

    return pd.DataFrame(data)


def clean_dataframe(df: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame, dict[str, Any]]:
    """Return (raw_copy, cleaned_df, validation_meta)."""
    raw = df.copy()
    cleaned = df.copy()

    for col in cleaned.columns:
        cleaned[col] = cleaned[col].map(_clean_cell)

    if "Tool_No" in cleaned.columns:
        cleaned["Tool_No"] = cleaned["Tool_No"].map(normalize_tool_no)

    # Drop empty rows and footer/stamp noise.
    mask = cleaned["Tool_No"].astype(str).str.len() > 0
    cleaned = cleaned.loc[mask].copy()

    footer_mask = cleaned.apply(
        lambda r: any(m in " ".join(r.astype(str)).lower() for m in FOOTER_MARKERS),
        axis=1,
    )
    cleaned = cleaned.loc[~footer_mask].copy()

    valid_mask = cleaned["Tool_No"].map(is_valid_tool_row)
    invalid = cleaned.loc[~valid_mask]
    cleaned = cleaned.loc[valid_mask].copy()

    # Merge continuation rows (blank Tool_No but other fields filled).
    merged_rows: list[dict[str, str]] = []
    for _, row in cleaned.iterrows():
        rec = {c: str(row.get(c, "")) for c in CANONICAL_COLUMNS}
        if merged_rows and not rec["Tool_No"] and any(rec[c] for c in CANONICAL_COLUMNS if c != "Tool_No"):
            prev = merged_rows[-1]
            for c in CANONICAL_COLUMNS:
                if rec[c] and c != "Tool_No":
                    prev[c] = (prev[c] + " " + rec[c]).strip()
            continue
        if rec["Tool_No"]:
            merged_rows.append(rec)

    if merged_rows:
        cleaned = pd.DataFrame(merged_rows, columns=CANONICAL_COLUMNS)

    meta = {
        "row_count_raw": len(raw),
        "row_count_cleaned": len(cleaned),
        "invalid_tool_no_rows": len(invalid),
        "tool_numbers": cleaned["Tool_No"].tolist() if "Tool_No" in cleaned.columns else [],
    }
    return raw, cleaned, meta
