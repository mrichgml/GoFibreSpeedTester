namespace GoFibreSpeedTester.Models;

public sealed record SpeedTestResult(
  DateTimeOffset TimestampUtc,
  double? DownloadMbps,
  double? UploadMbps,
  double? PingMs,
  double? JitterMs,
  double? PacketLossPercent,
  string? Notes,
  string? RawDownload,
  string? RawUpload,
  string? RawPing,
  string? RawJitter,
  string? RawPacketLoss
);

