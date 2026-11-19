# Đồng bộ bản sao chỉ để CHỤP batchmode (M7b, 44 quyết 2026-11-19): Editor người dùng mở liên tục trên bản chính
# nên batchmode chạy trên D:\Buzzy_Bus_capture. Chỉ chép Assets / Packages / ProjectSettings / UserSettings (robocopy /MIR),
# không chép Library / Temp / Logs / obj / .git. KHÔNG sửa file trong bản sao; mọi sửa ở bản chính rồi chạy lại script này.
# Dùng: powershell -ExecutionPolicy Bypass -File tools\sync_capture.ps1 [-Source D:\Buzzy_Bus] [-Target D:\Buzzy_Bus_capture]
param(
    [string]$Source = "D:\Buzzy_Bus",
    [string]$Target = "D:\Buzzy_Bus_capture",
    [switch]$PullConfig   # chép NGƯỢC đúng 1 file Res/Config/BuzzyLayoutConfig.asset từ bản sao về bản chính (sau SyncConfigDefaults chạy trên bản sao)
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $Source)) { throw "Không thấy bản chính $Source" }
if ($PullConfig) {
    $rel = "Assets\02_Gameplay\Res\Config\BuzzyLayoutConfig.asset"
    $from = Join-Path $Target $rel; $to = Join-Path $Source $rel
    if (-not (Test-Path $from)) { throw "Không thấy $from" }
    Copy-Item $from $to -Force
    Write-Host ("sync_capture: chép ngược {0} -> bản chính" -f $rel)
    exit 0
}
New-Item -ItemType Directory -Force $Target | Out-Null
$total = 0
foreach ($d in @("Assets", "Packages", "ProjectSettings", "UserSettings", "SDKs")) {   # SDKs: tarball package local mà Packages/manifest.json trỏ tới (chỉ chép, không đọc)
    $src = Join-Path $Source $d
    if (-not (Test-Path $src)) { continue }
    $dst = Join-Path $Target $d
    # /MIR phản chiếu (xoá file thừa ở đích), /XD .git, /NFL /NDL bớt log, /NJH /NJS bỏ header/summary dài
    # /XF: KHÔNG chép file bí mật (AGENTS luật 3); bản sao chỉ compile + chụp, không cần cấu hình thật
    $out = robocopy $src $dst /MIR /XD ".git" /XF "google-services.json" "GoogleService-Info.plist" "ApiKey.txt" "*.keystore" "*.jks" /R:2 /W:1 /NFL /NDL /NJH /NP /BYTES
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "robocopy $d lỗi mã $code" }
    # dòng "Files :" trong summary: tổng / copied / skipped / mismatch / failed / extras
    $line = ($out | Where-Object { $_ -match "^\s*Files\s*:" }) | Select-Object -Last 1
    if ($line) {
        $nums = [regex]::Matches($line, "\d+") | ForEach-Object { [int]$_.Value }
        if ($nums.Count -ge 2) { $total += $nums[1]; Write-Host ("{0,-16} chép {1,6} / tổng {2}" -f $d, $nums[1], $nums[0]) }
    }
}
# không chép .git; đảm bảo bản sao không thành repo
if (Test-Path (Join-Path $Target ".git")) { Remove-Item -Recurse -Force (Join-Path $Target ".git") }
Write-Host ("sync_capture: {0} file đổi -> {1}" -f $total, $Target)
exit 0
