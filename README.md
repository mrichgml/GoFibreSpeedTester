# GoFibreSpeedTester

Cross-platform (Windows/Linux) C# tool to run a web-based fibre speed check at a configurable interval and append results to a CSV file.

## What it does

- Runs forever (until stopped)
- Loads the configured speed test web page
- (Optionally) clicks cookie accept + start test
- Waits for results
- Extracts results via CSS selectors
- Appends a row to a CSV file

## Prereqs

- .NET SDK 8+
- Playwright browser binaries installed (Chromium by default)

After building once, install the browsers:

```bash
dotnet build .\GoFibreSpeedTester.slnx
pwsh .\src\GoFibreSpeedTester\bin\Debug\net8.0\playwright.ps1 install
```

On Linux, use:

```bash
dotnet build ./GoFibreSpeedTester.slnx
pwsh ./src/GoFibreSpeedTester/bin/Debug/net8.0/playwright.ps1 install --with-deps
```

## Configure

Edit `src/GoFibreSpeedTester/appsettings.json`:

- `SpeedTest:Url`: your speed test page URL
- `SpeedTest:IntervalMinutes`: default 5 minutes
- `SpeedTest:OutputCsvPath`: where to write the CSV (relative or absolute)
- `SpeedTest:Page:*Selector`: CSS selectors for your page:
  - `AcceptCookiesSelector` (optional)
  - `StartSelector` (optional)
  - `ResultsReadySelector` (recommended)
  - `DownloadSelector`, `UploadSelector`, `PingSelector`, etc.

Tip: open DevTools in your browser and use **Copy selector** on the element that contains the numeric value.
If you leave selectors blank, the tool will try best-effort automation (clicking a likely Start/Go button) and will attempt to extract values from the page text using keywords like “download”, “upload”, and “ping”.

## Run

```bash
dotnet run --project .\src\GoFibreSpeedTester\GoFibreSpeedTester.csproj
```

## Deployment (self-contained / standalone)

This produces a **self-contained** build (no .NET install required on the target machine).

### Windows x64

```powershell
pwsh .\publish.ps1 -Rid win-x64
```

### Linux x64

```bash
./publish.sh linux-x64 Release
```

### (Optional) install browsers during publish

If you want the publish output folder to already include the installed Playwright browsers:

- Windows:

```powershell
pwsh .\publish.ps1 -Rid win-x64 -InstallBrowsers
```

- Linux:

```bash
./publish.sh linux-x64 Release --install-browsers
```

### Install Playwright browsers on the target machine

From the published output folder:

- Windows / Linux:

```bash
pwsh ./playwright.ps1 install
```

- Linux (recommended for fresh machines):

```bash
pwsh ./playwright.ps1 install --with-deps
```

## CSV format

Columns (all quoted):

- `timestampUtc` (ISO-8601)
- `downloadMbps`
- `uploadMbps`
- `pingMs`
- `jitterMs`
- `packetLossPercent`
- `notes` (filled on failures)
- `rawDownload`, `rawUpload`, `rawPing`, `rawJitter`, `rawPacketLoss`

## Low download snapshots

If `downloadMbps` is below the configured threshold, the tool saves an image of the rendered OpenSpeedTest SVG UI:

- `SpeedTest:LowDownloadThresholdMbps` (default `300.0`)
- `SpeedTest:LowDownloadSnapshotSubfolder` (default `low-download`)
- `SpeedTest:SaveLowDownloadSnapshot` (default `true`)

The image filename is generated from the run time (local time): `yyyyMMdd_HHmmss.png`.

## Tests (MSTest)

```bash
dotnet test .\GoFibreSpeedTester.slnx
```

