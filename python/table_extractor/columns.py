"""Canonical tooling table columns for Master Tooling List PDFs."""

from __future__ import annotations

import re
from typing import Iterable

CANONICAL_COLUMNS: list[str] = [
    "Tool_No",
    "Tool_Name",
    "Consumable_Tool_Description",
    "Tool_Supplier",
    "Tool_Identifier",
    "Total_Diameter",
    "Flute_Length",
    "Total_Length",
    "Total_Corner_Radius",
    "Anchor_Description",
    "Tool_Path_Time_In_Minutes",
    "Remarks",
]

# Map normalized header text fragments to canonical column names.
HEADER_ALIASES: dict[str, str] = {
    "tool no": "Tool_No",
    "tool #": "Tool_No",
    "tool name": "Tool_Name",
    "consumable": "Consumable_Tool_Description",
    "tool supplier": "Tool_Supplier",
    "supplier": "Tool_Supplier",
    "tool identifier": "Tool_Identifier",
    "tool holder": "Tool_Identifier",
    "holder": "Tool_Identifier",
    "total diameter": "Total_Diameter",
    "tool diameter": "Total_Diameter",
    "diameter (d1)": "Total_Diameter",
    "d1": "Total_Diameter",
    "flute length": "Flute_Length",
    "flute length (l1)": "Flute_Length",
    "l1": "Flute_Length",
    "total length": "Total_Length",
    "tool ext": "Total_Length",
    "ext. length": "Total_Length",
    "ext length": "Total_Length",
    "l2": "Total_Length",
    "total corner": "Total_Corner_Radius",
    "corner radius": "Total_Corner_Radius",
    "cornerradius": "Total_Corner_Radius",
    "anchor": "Anchor_Description",
    "arbor": "Anchor_Description",
    "tool path time": "Tool_Path_Time_In_Minutes",
    "path time": "Tool_Path_Time_In_Minutes",
    "minutes": "Tool_Path_Time_In_Minutes",
    "remarks": "Remarks",
}

# T01-style or SECO numeric (10, 11, …) tool numbers.
TOOL_NO_PATTERN = re.compile(r"^(?:T\d{2}|\d{2})$", re.IGNORECASE)
FOOTER_MARKERS = (
    "cam programmer",
    "approved by",
    "tool register",
    "tool registered",
    "signature",
    "stamp",
)


def normalize_header_cell(value: str | None) -> str:
    if value is None:
        return ""
    text = re.sub(r"\s+", " ", str(value).strip().lower())
    return text


def map_headers(header_cells: Iterable[str | None]) -> list[str | None]:
    """Map raw header cells to canonical column names (or None if unknown)."""
    mapped: list[str | None] = []
    for cell in header_cells:
        norm = normalize_header_cell(cell)
        if not norm:
            mapped.append(None)
            continue
        hit: str | None = None
        for fragment, col in HEADER_ALIASES.items():
            if fragment in norm:
                hit = col
                break
        mapped.append(hit)
    return mapped


def is_footer_row(row: list[str | None]) -> bool:
    joined = " ".join(str(c or "") for c in row).lower()
    return any(marker in joined for marker in FOOTER_MARKERS)


def is_tool_header_row(row: list[str | None]) -> bool:
    joined = " ".join(str(c or "") for c in row).lower()
    return "tool" in joined and ("no" in joined or "#" in joined or "name" in joined)
