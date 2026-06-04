# PaddleOCR PP-OCRv4 Model Files (Offline Bundle)

The application loads OCR models **only** from this directory. It will **not** download models at runtime.

## Required layout

```
models/ppocr_v4/
├── det/
│   ├── inference.pdmodel
│   └── inference.pdiparams
├── cls/
│   ├── inference.pdmodel
│   └── inference.pdiparams
└── rec/
    ├── inference.pdmodel
    └── inference.pdiparams
```

## Download (run once on a machine with internet)

**Quick setup (Windows, from `ToolingExtractor` folder):**

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\download-models.ps1
```

Manual URLs (English, per PaddleOCR 2.10):

1. Detection: `en_PP-OCRv3_det_infer.tar` — https://paddleocr.bj.bcebos.com/PP-OCRv3/english/en_PP-OCRv3_det_infer.tar  
2. Recognition: `en_PP-OCRv4_rec_infer.tar` — https://paddleocr.bj.bcebos.com/PP-OCRv4/english/en_PP-OCRv4_rec_infer.tar  
3. Classification: `ch_ppocr_mobile_v2.0_cls_infer.tar` — https://paddleocr.bj.bcebos.com/dygraph_v2.0/ch/ch_ppocr_mobile_v2.0_cls_infer.tar  
4. Copy `en_dict.txt` into `rec/` (included by the script).

Extract each archive so `inference.pdmodel` and `inference.pdiparams` are directly under `det/`, `rec/`, and `cls/`.

Copy the entire `models/ppocr_v4` folder to air-gapped servers before starting ToolingExtractor.
