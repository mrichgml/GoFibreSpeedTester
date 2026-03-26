using System.ComponentModel.DataAnnotations;

namespace GoFibreSpeedTester;

public sealed class SpeedTestOptions
{
  public const string SectionName = "SpeedTest";

  [Required]
  public string Url { get; init; } = "";

  [Range(1, 24 * 60)]
  public int IntervalMinutes { get; init; } = 5;

  [Range(10, 24 * 60 * 60)]
  public int TimeoutSeconds { get; init; } = 180;

  public BrowserOptions Browser { get; init; } = new();
  public PageOptions Page { get; init; } = new();

  [Required]
  public string OutputCsvPath { get; init; } = "speed-results.csv";

  public bool AppendHeaderIfNewFile { get; init; } = true;
  public bool CaptureScreenshotOnFailure { get; init; } = true;
  public string FailureScreenshotPath { get; init; } = "last-failure.png";

  public double LowDownloadThresholdMbps { get; init; } = 300.0;
  public string LowDownloadSnapshotSubfolder { get; init; } = "low-download";
  public bool SaveLowDownloadSnapshot { get; init; } = true;

  public sealed class BrowserOptions
  {
    public bool Headless { get; init; } = true;

    /// <summary>
    /// One of: chromium, firefox, webkit
    /// </summary>
    public string BrowserName { get; init; } = "chromium";
  }

  public sealed class PageOptions
  {
    public string AcceptCookiesSelector { get; init; } = "";
    public string StartSelector { get; init; } = "";
    public string ResultsReadySelector { get; init; } = "";

    public string DownloadSelector { get; init; } = "";
    public string UploadSelector { get; init; } = "";
    public string PingSelector { get; init; } = "";
    public string JitterSelector { get; init; } = "";
    public string PacketLossSelector { get; init; } = "";
  }
}

