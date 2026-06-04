"""tabula-py fallback extraction."""

from __future__ import annotations

from .columns import is_tool_header_row, map_headers
from .pdfplumber_extract import _merge_header_rows, _score_table


def extract_with_tabula(pdf_path: str) -> tuple[list[list[str | None]] | None, list[list[str | None]] | None, str]:
    try:
        import tabula
    except ImportError as ex:
        return None, None, f"tabula: not installed ({ex})"

    try:
        dfs = tabula.read_pdf(pdf_path, pages="all", multiple_tables=True, lattice=True)
    except Exception as ex:
        try:
            dfs = tabula.read_pdf(pdf_path, pages="all", multiple_tables=True, stream=True)
        except Exception as ex2:
            return None, None, f"tabula: failed ({ex}; {ex2})"

    best_table = None
    best_score = 0
    for df in dfs:
        data = df.fillna("").astype(str).values.tolist()
        sc = _score_table(data)
        if sc > best_score:
            best_score = sc
            best_table = data

    if not best_table or best_score < 20:
        return None, None, "tabula: no tooling table detected"

    rows = [[c.strip() or None for c in row] for row in best_table]
    header_idx = next(
        (i for i, row in enumerate(rows[:5]) if is_tool_header_row([str(c or "") for c in row])),
        0,
    )
    header_cells, body = _merge_header_rows(rows, header_idx)
    return [header_cells], body, "tabula"
