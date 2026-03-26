param(
  [ValidateSet("win-x64", "linux-x64")]
  [string]$Rid = "win-x64",

  [ValidateSet("Release", "Debug")]
  [string]$Configuration = "Release",

  [switch]$SingleFile,
  [switch]$ReadyToRun,
  [switch]$Trim,
  [switch]$NoRestore,
  [switch]$InstallBrowsers
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\GoFibreSpeedTester\GoFibreSpeedTester.csproj"

$args = @(
  "publish", $project,
  "-c", $Configuration,
  "-r", $Rid,
  "--self-contained", "true"
)

if ($NoRestore) { $args += "--no-restore" }
if ($SingleFile) { $args += "-p:PublishSingleFile=true" }
if ($ReadyToRun) { $args += "-p:PublishReadyToRun=true" }
if ($Trim) { $args += "-p:PublishTrimmed=true" }

Write-Host "Publishing $Rid ($Configuration)..." -ForegroundColor Cyan
dotnet @args

$outDir = Join-Path $PSScriptRoot ("src\GoFibreSpeedTester\bin\{0}\net8.0\{1}\publish" -f $Configuration, $Rid)
Write-Host "Publish output:" -ForegroundColor Green
Write-Host "  $outDir"
Write-Host ""
if ($InstallBrowsers) {
  Write-Host "Installing Playwright browsers in publish folder..." -ForegroundColor Cyan
  Push-Location $outDir
  try {
    if ($Rid -like "linux-*") {
      pwsh -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 install --with-deps
    }
    else {
      pwsh -NoProfile -ExecutionPolicy Bypass -File .\playwright.ps1 install
    }
  }
  finally {
    Pop-Location
  }
}
else {
  Write-Host "Next step on the target machine (from the publish folder):" -ForegroundColor Yellow
  Write-Host "  pwsh ./playwright.ps1 install"
  if ($Rid -like "linux-*") {
    Write-Host "  (Linux) pwsh ./playwright.ps1 install --with-deps"
  }
}

