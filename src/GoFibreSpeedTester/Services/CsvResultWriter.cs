using System.Globalization;
using System.Text;
using GoFibreSpeedTester.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoFibreSpeedTester.Services;

public sealed class CsvResultWriter : IResultWriter
{
  private readonly SpeedTestOptions _options;
  private readonly ILogger<CsvResultWriter> _logger;

  public CsvResultWriter(IOptions<SpeedTestOptions> options, ILogger<CsvResultWriter> logger)
  {
    _options = options.Value;
    _logger = logger;
  }

  public async Task AppendAsync(SpeedTestResult result, CancellationToken cancellationToken)
  {
    var path = _options.OutputCsvPath;
    var fileExists = File.Exists(path);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

    await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
    await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    if (!fileExists && _options.AppendHeaderIfNewFile)
    {
      await writer.WriteLineAsync(CsvHeader());
    }

    await writer.WriteLineAsync(ToCsvLine(result));
    await writer.FlushAsync(cancellationToken);

    _logger.LogInformation("Appended result to {Path}", path);
  }

  private static string CsvHeader() =>
    string.Join(',',
      "timestampUtc",
      "downloadMbps",
      "uploadMbps",
      "pingMs",
      "jitterMs",
      "packetLossPercent",
      "notes",
      "rawDownload",
      "rawUpload",
      "rawPing",
      "rawJitter",
      "rawPacketLoss");

  private static string ToCsvLine(SpeedTestResult r)
  {
    static string F(double? v) => v?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    static string Q(string? s)
    {
      s ??= "";
      return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    return string.Join(',',
      Q(r.TimestampUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
      Q(F(r.DownloadMbps)),
      Q(F(r.UploadMbps)),
      Q(F(r.PingMs)),
      Q(F(r.JitterMs)),
      Q(F(r.PacketLossPercent)),
      Q(r.Notes),
      Q(r.RawDownload),
      Q(r.RawUpload),
      Q(r.RawPing),
      Q(r.RawJitter),
      Q(r.RawPacketLoss));
  }
}

