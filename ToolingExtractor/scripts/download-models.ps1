# One-time download of PP-OCRv4 English models into models/ppocr_v4/
# URLs match PaddleOCR release/2.10 paddleocr.py MODEL_URLS for lang=en, version=PP-OCRv4
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$ModelsRoot = Join-Path $Root "models\ppocr_v4"

$downloads = @(
    @{
        Url = "https://paddleocr.bj.bcebos.com/PP-OCRv3/english/en_PP-OCRv3_det_infer.tar"
        Target = "det"
    },
    @{
        Url = "https://paddleocr.bj.bcebos.com/PP-OCRv4/english/en_PP-OCRv4_rec_infer.tar"
        Target = "rec"
    },
    @{
        Url = "https://paddleocr.bj.bcebos.com/dygraph_v2.0/ch/ch_ppocr_mobile_v2.0_cls_infer.tar"
        Target = "cls"
    }
)

function Install-TarModel($url, $targetSubfolder) {
    $destDir = Join-Path $ModelsRoot $targetSubfolder
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $tarPath = Join-Path $env:TEMP ("ppocr_" + $targetSubfolder + ".tar")
    Write-Host "Downloading $targetSubfolder from $url ..."
    Invoke-WebRequest -Uri $url -OutFile $tarPath -UseBasicParsing
    $extractTemp = Join-Path $env:TEMP ("ppocr_extract_" + $targetSubfolder)
    if (Test-Path $extractTemp) { Remove-Item $extractTemp -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $extractTemp | Out-Null
    tar -xf $tarPath -C $extractTemp
    $inner = Get-ChildItem $extractTemp -Recurse -File -Filter "inference.pdmodel" | Select-Object -First 1
    if (-not $inner) {
        $inner = Get-ChildItem $extractTemp -Recurse -File -Filter "inference.json" | Select-Object -First 1
    }
    if (-not $inner) { throw "No inference model found in archive for $targetSubfolder" }
    $srcFolder = $inner.DirectoryName
    Get-ChildItem $srcFolder | Copy-Item -Destination $destDir -Force
    Remove-Item $tarPath -Force -ErrorAction SilentlyContinue
    Remove-Item $extractTemp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Installed $targetSubfolder -> $destDir"
}

foreach ($d in $downloads) {
    Install-TarModel $d.Url $d.Target
}

$recDir = Join-Path $ModelsRoot "rec"
$dictUrl = "https://raw.githubusercontent.com/PaddlePaddle/PaddleOCR/release/2.10/ppocr/utils/en_dict.txt"
$dictPath = Join-Path $recDir "en_dict.txt"
if (-not (Test-Path $dictPath)) {
    Write-Host "Downloading en_dict.txt ..."
    Invoke-WebRequest -Uri $dictUrl -OutFile $dictPath -UseBasicParsing
}

Write-Host "Done. Model root: $ModelsRoot"
Write-Host "Verify: det, cls, rec each contain inference.pdmodel (or inference.json) + inference.pdiparams"
