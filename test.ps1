$ErrorActionPreference = 'Stop'
$application = Join-Path $PSScriptRoot '鸣潮终焉矩阵配队工具.exe'
if (-not (Test-Path -LiteralPath $application)) {
    & (Join-Path $PSScriptRoot 'source\build.ps1')
}
$resultsDirectory = Join-Path $PSScriptRoot 'tests\results'
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
$report = Join-Path $resultsDirectory 'self-test.txt'
$process = Start-Process -FilePath $application -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) {
    if (Test-Path -LiteralPath $report) { Get-Content -Encoding utf8 -LiteralPath $report }
    throw ('Self-test failed with exit code ' + $process.ExitCode)
}
Get-Content -Encoding utf8 -LiteralPath $report
