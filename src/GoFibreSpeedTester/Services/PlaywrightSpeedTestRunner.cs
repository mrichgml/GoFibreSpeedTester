using GoFibreSpeedTester.Models;
using GoFibreSpeedTester.Utils;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace GoFibreSpeedTester.Services;

public sealed class PlaywrightSpeedTestRunner : ISpeedTestRunner
{
  private readonly SpeedTestOptions _options;
  private readonly ILogger<PlaywrightSpeedTestRunner> _logger;

  public PlaywrightSpeedTestRunner(IOptions<SpeedTestOptions> options, ILogger<PlaywrightSpeedTestRunner> logger)
  {
    _options = options.Value;
    _logger = logger;
  }

  public async Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken)
  {
    var ts = DateTimeOffset.UtcNow;

    using var playwright = await Playwright.CreateAsync();
    var browser = await LaunchBrowserAsync(playwright);
    await using var _ = browser;

    var context = await browser.NewContextAsync(new BrowserNewContextOptions
    {
      IgnoreHTTPSErrors = true
    });

    var page = await context.NewPageAsync();
    page.SetDefaultTimeout(_options.TimeoutSeconds * 1000);

    try
    {
      _logger.LogInformation("Navigating to {Url}", _options.Url);
      var effectiveUrl = _options.Url;
      if (string.IsNullOrWhiteSpace(_options.Page.StartSelector))
      {
        // OpenSpeedTest supports auto-run via `?Run` (or `&Run`). If we're not configured to click Start,
        // prefer auto-run to avoid writing blank rows.
        effectiveUrl = EnsureQueryFlag(effectiveUrl, "Run");
      }

      await page.GotoAsync(effectiveUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

      if (!string.IsNullOrWhiteSpace(_options.Page.AcceptCookiesSelector))
      {
        await TryClickAsync(page, _options.Page.AcceptCookiesSelector, "accept cookies", cancellationToken);
      }
      else
      {
        await TryClickByTextAsync(page, "accept cookies", new[] { "accept", "agree" }, cancellationToken);
      }

      if (!string.IsNullOrWhiteSpace(_options.Page.StartSelector))
      {
        await TryClickAsync(page, _options.Page.StartSelector, "start test", cancellationToken);
      }
      else
      {
        await TryClickByTextAsync(page, "start test", new[] { "start", "go", "begin" }, cancellationToken);
        await TryPressEnterAsync(page, cancellationToken);
      }

      if (!string.IsNullOrWhiteSpace(_options.Page.ResultsReadySelector))
      {
        _logger.LogInformation("Waiting for results selector {Selector}", _options.Page.ResultsReadySelector);
        await page.WaitForSelectorAsync(_options.Page.ResultsReadySelector, new PageWaitForSelectorOptions
        {
          State = WaitForSelectorState.Visible,
          Timeout = _options.TimeoutSeconds * 1000
        });
      }
      else
      {
        _logger.LogWarning("No ResultsReadySelector configured; waiting for non-zero results in the OpenSpeedTest UI.");
        await WaitForNonZeroResultsAsync(page, _options.TimeoutSeconds, cancellationToken);
      }

      var rawDownload = await ReadTextAsync(page, _options.Page.DownloadSelector, cancellationToken);
      var rawUpload = await ReadTextAsync(page, _options.Page.UploadSelector, cancellationToken);
      var rawPing = await ReadTextAsync(page, _options.Page.PingSelector, cancellationToken);
      var rawJitter = await ReadTextAsync(page, _options.Page.JitterSelector, cancellationToken);
      var rawPacketLoss = await ReadTextAsync(page, _options.Page.PacketLossSelector, cancellationToken);

      // If selectors were not configured (or failed), fall back to parsing the page text.
      string? pageText = null;
      if (rawDownload is null && rawUpload is null && rawPing is null && rawJitter is null && rawPacketLoss is null)
      {
        // OpenSpeedTest renders UI inside an embedded SVG <object id="OpenSpeedTest-UI">.
        // Try SVG first, then body text as a fallback.
        pageText = await TryReadOpenSpeedTestUiTextAsync(page, cancellationToken)
                   ?? await TryReadBodyTextAsync(page, cancellationToken);
        if (!string.IsNullOrWhiteSpace(pageText))
        {
          // First try id-based extraction from the SVG DOM (more reliable than keywords).
          var idMap = await TryReadOpenSpeedTestUiIdTextMapAsync(page, cancellationToken);
          // OpenSpeedTest IDs (as seen in app.svg) commonly include: downResult, upRestxt, pingResult, jitterDesk.
          rawDownload ??= TryPickValueByIdHints(idMap, new[] { "downresult", "download", "dl", "down", "dlspeed" });
          rawUpload ??= TryPickValueByIdHints(idMap, new[] { "uprestxt", "upresult", "upload", "ul", "up", "ulspeed" });
          rawPing ??= TryPickValueByIdHints(idMap, new[] { "pingresult", "ping", "latency" });
          rawJitter ??= TryPickValueByIdHints(idMap, new[] { "jitterdesk", "jitter" });
          rawPacketLoss ??= TryPickValueByIdHints(idMap, new[] { "loss", "packet" });

          rawDownload ??= TryExtractLabeledValue(pageText, "download");
          rawUpload ??= TryExtractLabeledValue(pageText, "upload");
          rawPing ??= TryExtractLabeledValue(pageText, "ping");
          rawJitter ??= TryExtractLabeledValue(pageText, "jitter");
          rawPacketLoss ??= TryExtractLabeledValue(pageText, "packet loss");

          // Final fallback: extract by units (OpenSpeedTest commonly shows "Mbps" and "ms").
          if (rawDownload is null || rawUpload is null || rawPing is null || rawJitter is null || rawPacketLoss is null)
          {
            var byUnits = TryExtractByUnits(pageText);
            rawDownload ??= byUnits.RawDownload;
            rawUpload ??= byUnits.RawUpload;
            rawPing ??= byUnits.RawPing;
            rawJitter ??= byUnits.RawJitter;
            rawPacketLoss ??= byUnits.RawPacketLoss;
          }
        }
      }

      var result = new SpeedTestResult(
        TimestampUtc: ts,
        DownloadMbps: ValueParsing.ParseNullableDouble(rawDownload),
        UploadMbps: ValueParsing.ParseNullableDouble(rawUpload),
        PingMs: ValueParsing.ParseNullableDouble(rawPing),
        JitterMs: ValueParsing.ParseNullableDouble(rawJitter),
        PacketLossPercent: ValueParsing.ParseNullablePercent(rawPacketLoss),
        Notes: null,
        RawDownload: rawDownload,
        RawUpload: rawUpload,
        RawPing: rawPing,
        RawJitter: rawJitter,
        RawPacketLoss: rawPacketLoss
      );

      await SaveLowDownloadSnapshotIfNeededAsync(page, result, cancellationToken);

      return result;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Speed test run failed.");

      if (_options.CaptureScreenshotOnFailure)
      {
        try
        {
          await page.ScreenshotAsync(new PageScreenshotOptions
          {
            Path = _options.FailureScreenshotPath,
            FullPage = true
          });
          _logger.LogInformation("Saved failure screenshot to {Path}", _options.FailureScreenshotPath);
        }
        catch (Exception screenshotEx)
        {
          _logger.LogWarning(screenshotEx, "Failed to capture screenshot.");
        }
      }

      return new SpeedTestResult(
        TimestampUtc: ts,
        DownloadMbps: null,
        UploadMbps: null,
        PingMs: null,
        JitterMs: null,
        PacketLossPercent: null,
        Notes: ex.GetType().Name + ": " + ex.Message,
        RawDownload: null,
        RawUpload: null,
        RawPing: null,
        RawJitter: null,
        RawPacketLoss: null
      );
    }
  }

  private async Task SaveLowDownloadSnapshotIfNeededAsync(IPage page, SpeedTestResult result, CancellationToken cancellationToken)
  {
    if (!_options.SaveLowDownloadSnapshot) return;
    if (result.DownloadMbps is null) return;
    if (result.DownloadMbps.Value >= _options.LowDownloadThresholdMbps) return;

    try
    {
      var csvDir = Path.GetDirectoryName(Path.GetFullPath(_options.OutputCsvPath)) ?? AppContext.BaseDirectory;
      var folder = Path.Combine(csvDir, _options.LowDownloadSnapshotSubfolder);
      Directory.CreateDirectory(folder);

      var stamp = result.TimestampUtc.ToLocalTime().ToString("yyyyMMdd_HHmmss");
      var fileName = $"{stamp}.png";
      var outPath = Path.Combine(folder, fileName);

      var svgObj = page.Locator("#OpenSpeedTest-UI").First;
      if (await svgObj.CountAsync() == 0)
      {
        // Fallback to full page screenshot if the object isn't present for some reason.
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = outPath, FullPage = true });
      }
      else
      {
        await svgObj.ScreenshotAsync(new LocatorScreenshotOptions { Path = outPath });
      }

      _logger.LogWarning(
        "Low download {DownloadMbps} Mbps (<{Threshold}). Saved SVG snapshot to {Path}",
        result.DownloadMbps.Value,
        _options.LowDownloadThresholdMbps,
        outPath);
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Failed to save low-download SVG snapshot.");
    }
  }

  private async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright)
  {
    var headless = _options.Browser.Headless;
    var name = (_options.Browser.BrowserName ?? "chromium").Trim().ToLowerInvariant();

    return name switch
    {
      "firefox" => await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless }),
      "webkit" => await playwright.Webkit.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless }),
      _ => await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless }),
    };
  }

  private static async Task TryClickAsync(IPage page, string selector, string purpose, CancellationToken cancellationToken)
  {
    try
    {
      var loc = page.Locator(selector);
      await loc.First.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
    }
    catch
    {
      // Best-effort; some pages won't show cookie prompt/start button every time.
    }
  }

  private static async Task TryClickByTextAsync(IPage page, string purpose, string[] words, CancellationToken cancellationToken)
  {
    try
    {
      // Try buttons first.
      foreach (var w in words)
      {
        var button = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = w, Exact = false }).First;
        if (await button.CountAsync() > 0)
        {
          await button.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
          return;
        }
      }
    }
    catch
    {
      // ignore
    }

    try
    {
      // Fallback: any element containing the word.
      foreach (var w in words)
      {
        var loc = page.Locator($":text-matches(\"{Regex.Escape(w)}\", \"i\")").First;
        if (await loc.CountAsync() > 0)
        {
          await loc.ClickAsync(new LocatorClickOptions { Timeout = 5000 });
          return;
        }
      }
    }
    catch
    {
      // ignore
    }
  }

  private static async Task TryPressEnterAsync(IPage page, CancellationToken cancellationToken)
  {
    try
    {
      await page.Keyboard.PressAsync("Enter");
    }
    catch
    {
      // ignore
    }
  }

  private static async Task<string?> ReadTextAsync(IPage page, string selector, CancellationToken cancellationToken)
  {
    if (string.IsNullOrWhiteSpace(selector)) return null;
    var loc = page.Locator(selector).First;
    try
    {
      return (await loc.InnerTextAsync()).Trim();
    }
    catch
    {
      return null;
    }
  }

  private static async Task WaitForAnyKeywordAsync(IPage page, string[] keywords, int timeoutSeconds, CancellationToken cancellationToken)
  {
    var deadlineMs = Math.Max(5_000, timeoutSeconds * 1000);
    var pattern = string.Join("|", keywords.Select(Regex.Escape));

    try
    {
      await page.WaitForFunctionAsync(
        // Check both HTML body text and the OpenSpeedTest embedded SVG UI text (if present).
        $"() => {{ " +
        $"  const bodyText = document.body?.innerText || ''; " +
        $"  const obj = document.querySelector('#OpenSpeedTest-UI'); " +
        $"  const svgText = obj && obj.contentDocument ? (obj.contentDocument.documentElement?.textContent || '') : ''; " +
        $"  return /{pattern}/i.test(bodyText) || /{pattern}/i.test(svgText); " +
        $"}}",
        null,
        new PageWaitForFunctionOptions { Timeout = deadlineMs });
    }
    catch
    {
      // Best effort.
    }
  }

  private static async Task WaitForNonZeroResultsAsync(IPage page, int timeoutSeconds, CancellationToken cancellationToken)
  {
    var deadlineMs = Math.Max(5_000, timeoutSeconds * 1000);

    // Wait until we can see BOTH download+upload Mbps values > 0.1 inside the embedded SVG UI.
    try
    {
      await page.WaitForFunctionAsync(
        "() => {\n" +
        "  const obj = document.querySelector('#OpenSpeedTest-UI');\n" +
        "  const doc = obj && obj.contentDocument ? obj.contentDocument : null;\n" +
        "  const root = doc ? doc.documentElement : null;\n" +
        "  const t = root ? (root.textContent || '') : (document.body?.innerText || '');\n" +
        "  const nums = (t.match(/(\\d+(?:[.,]\\d+)?)\\s*Mbps/gi) || [])\n" +
        "    .map(s => parseFloat(s.replace(/[^0-9,\\.\\-\\+]/g, '').replace(',', '.')))\n" +
        "    .filter(n => !isNaN(n) && n > 0.1);\n" +
        "  if (nums.length >= 2) return true;\n" +
        "  // Prefer the known IDs if present.\n" +
        "  const down = doc ? doc.querySelector('#downResult') : null;\n" +
        "  const up = doc ? doc.querySelector('#upRestxt') : null;\n" +
        "  const dn = down ? parseFloat((down.textContent || '').trim().replace(',', '.')) : NaN;\n" +
        "  const un = up ? parseFloat((up.textContent || '').trim().replace(',', '.')) : NaN;\n" +
        "  return !isNaN(dn) && dn > 0.1 && !isNaN(un) && un > 0.1;\n" +
        "}",
        null,
        new PageWaitForFunctionOptions { Timeout = deadlineMs });
    }
    catch
    {
      // Best effort.
    }
  }

  private static async Task<string?> TryReadBodyTextAsync(IPage page, CancellationToken cancellationToken)
  {
    try
    {
      return await page.Locator("body").InnerTextAsync();
    }
    catch
    {
      return null;
    }
  }

  private static async Task<string?> TryReadOpenSpeedTestUiTextAsync(IPage page, CancellationToken cancellationToken)
  {
    try
    {
      return await page.EvaluateAsync<string?>(
        "() => {\n" +
        "  const obj = document.querySelector('#OpenSpeedTest-UI');\n" +
        "  const doc = obj && obj.contentDocument ? obj.contentDocument : null;\n" +
        "  if (!doc) return null;\n" +
        "  const root = doc.documentElement;\n" +
        "  const t = (root && (root.innerText || root.textContent)) || '';\n" +
        "  return (t || '').trim() || null;\n" +
        "}");
    }
    catch
    {
      return null;
    }
  }

  private static async Task<Dictionary<string, string>> TryReadOpenSpeedTestUiIdTextMapAsync(IPage page, CancellationToken cancellationToken)
  {
    try
    {
      return await page.EvaluateAsync<Dictionary<string, string>>(
        "() => {\n" +
        "  const result = {};\n" +
        "  const obj = document.querySelector('#OpenSpeedTest-UI');\n" +
        "  const doc = obj && obj.contentDocument ? obj.contentDocument : null;\n" +
        "  if (!doc) return result;\n" +
        "  const nodes = doc.querySelectorAll('[id]');\n" +
        "  for (const n of nodes) {\n" +
        "    const id = (n.getAttribute('id') || '').trim();\n" +
        "    if (!id) continue;\n" +
        "    const txt = (n.textContent || '').trim();\n" +
        "    if (!txt) continue;\n" +
        "    if (!(id in result)) result[id] = txt;\n" +
        "  }\n" +
        "  return result;\n" +
        "}");
    }
    catch
    {
      return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
  }

  private static string? TryPickValueByIdHints(Dictionary<string, string> idMap, string[] hints)
  {
    if (idMap.Count == 0) return null;
    foreach (var kv in idMap)
    {
      var id = kv.Key;
      foreach (var h in hints)
      {
        if (id.Contains(h, StringComparison.OrdinalIgnoreCase))
        {
          return kv.Value;
        }
      }
    }
    return null;
  }

  private static (string? RawDownload, string? RawUpload, string? RawPing, string? RawJitter, string? RawPacketLoss) TryExtractByUnits(string text)
  {
    static List<string> Matches(string pattern, string input)
      => Regex.Matches(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline)
        .Select(m => m.Groups["val"].Value)
        .Where(v => !string.IsNullOrWhiteSpace(v))
        .ToList();

    var mbps = Matches(@"(?<val>[-+]?\d+(?:[.,]\d+)?)\s*Mbps\b", text);
    var ms = Matches(@"(?<val>[-+]?\d+(?:[.,]\d+)?)\s*ms\b", text);
    var pct = Matches(@"(?<val>[-+]?\d+(?:[.,]\d+)?)\s*%\b", text);

    return (
      RawDownload: mbps.Count > 0 ? mbps[0] : null,
      RawUpload: mbps.Count > 1 ? mbps[1] : null,
      RawPing: ms.Count > 0 ? ms[0] : null,
      RawJitter: ms.Count > 1 ? ms[1] : null,
      RawPacketLoss: pct.Count > 0 ? pct[0] : null
    );
  }

  private static string? TryExtractLabeledValue(string pageText, string label)
  {
    // Finds e.g. "Download 123.45" or "Packet loss 0%"
    var rx = new Regex($@"(?is)\b{Regex.Escape(label)}\b[\s:]*([-+]?\d+(?:[.,]\d+)?)");
    var m = rx.Match(pageText);
    return m.Success ? m.Groups[1].Value : null;
  }

  private static string EnsureQueryFlag(string url, string flag)
  {
    if (string.IsNullOrWhiteSpace(url)) return url;
    if (url.Contains($"{flag}=", StringComparison.OrdinalIgnoreCase)) return url;
    if (url.Contains($"?{flag}", StringComparison.OrdinalIgnoreCase) || url.Contains($"&{flag}", StringComparison.OrdinalIgnoreCase)) return url;
    return url.Contains('?') ? $"{url}&{flag}" : $"{url}?{flag}";
  }
}

