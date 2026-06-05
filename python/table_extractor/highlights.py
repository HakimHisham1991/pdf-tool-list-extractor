"""Extract normalized highlight boxes from PDF pages (table edges, ignored bands)."""

from __future__ import annotations

import re
from typing import Any

import pdfplumber

TOOL_ROW_RE = re.compile(r"^T(\d{2})\b", re.IGNORECASE)


def _norm_box(x0: float, top: float, x1: float, bottom: float, pw: float, ph: float, box_type: str, label: str = "", confidence: float = 1.0) -> dict[str, Any]:
    return {
        "x": max(0.0, min(1.0, x0 / pw)),
        "y": max(0.0, min(1.0, top / ph)),
        "width": max(0.0, min(1.0, (x1 - x0) / pw)),
        "height": max(0.0, min(1.0, (bottom - top) / ph)),
        "type": box_type,
        "confidence": confidence,
        "label": label,
    }


def extract_page_highlights(pdf_path: str) -> list[dict[str, Any]]:
    """Return per-page highlight payloads: {pageNumber, boxes}."""
    pages_out: list[dict[str, Any]] = []

    with pdfplumber.open(pdf_path) as pdf:
        for page_num, page in enumerate(pdf.pages, start=1):
            pw = float(page.width)
            ph = float(page.height)
            boxes: list[dict[str, Any]] = []

            boxes.append(_norm_box(0, 0, pw, ph * 0.12, pw, ph, "ignored", "header band"))
            boxes.append(_norm_box(0, ph * 0.88, pw, ph, pw, ph, "ignored", "footer band"))

            for edge in page.edges:
                w = abs(edge.get("width", 0))
                h = edge.get("height", 0)
                if w < 0.5 and h < 8:
                    continue
                if h < 0.5 and w < 8:
                    continue
                x0 = float(edge["x0"])
                x1 = float(edge["x1"])
                top = float(edge["top"])
                bottom = float(edge["bottom"])
                if x1 - x0 < 1 and bottom - top < 1:
                    continue
                boxes.append(_norm_box(x0, top, x1, bottom, pw, ph, "table", "border"))

            words = page.extract_words(x_tolerance=2, y_tolerance=3, keep_blank_chars=False)
            lines: dict[float, list[dict[str, Any]]] = {}
            for w in words:
                y = round(float(w["top"]), 1)
                lines.setdefault(y, []).append(w)

            table_words: list[dict[str, Any]] = []
            for y in sorted(lines.keys()):
                text = " ".join(x["text"] for x in sorted(lines[y], key=lambda w: w["x0"]))
                if TOOL_ROW_RE.match(text.strip()) or "tool no" in text.lower():
                    table_words.extend(lines[y])

            if len(table_words) >= 3:
                xs = [float(w["x0"]) for w in table_words] + [float(w["x1"]) for w in table_words]
                ys = [float(w["top"]) for w in table_words] + [float(w["bottom"]) for w in table_words]
                pad = 6.0
                boxes.append(
                    _norm_box(
                        min(xs) - pad,
                        min(ys) - pad,
                        max(xs) + pad,
                        max(ys) + pad,
                        pw,
                        ph,
                        "table",
                        "tool table",
                    )
                )

            if len(boxes) > 1200:
                boxes = boxes[:1200]

            pages_out.append({"pageNumber": page_num, "boxes": boxes})

    return pages_out
