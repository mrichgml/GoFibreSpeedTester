using GoFibreSpeedTester.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoFibreSpeedTester.Services;

public sealed class SpeedTestWorker : BackgroundService
{
  private readonly SpeedTestOptions _options;
  private readonly ISpeedTestRunner _runner;
  private readonly IResultWriter _writer;
  private readonly ILogger<SpeedTestWorker> _logger;

  public SpeedTestWorker(
    IOptions<SpeedTestOptions> options,
    ISpeedTestRunner runner,
    IResultWriter writer,
    ILogger<SpeedTestWorker> logger)
  {
    _options = options.Value;
    _runner = runner;
    _writer = writer;
    _logger = logger;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var interval = TimeSpan.FromMinutes(_options.IntervalMinutes);
    _logger.LogInformation("Starting worker. Interval={Interval}. Output={Csv}", interval, _options.OutputCsvPath);

    while (!stoppingToken.IsCancellationRequested)
    {
      var started = DateTimeOffset.UtcNow;

      SpeedTestResult result = await _runner.RunAsync(stoppingToken);
      await _writer.AppendAsync(result, stoppingToken);

      var elapsed = DateTimeOffset.UtcNow - started;
      var delay = interval - elapsed;
      if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

      _logger.LogInformation("Next run in {Delay}", delay);
      await Task.Delay(delay, stoppingToken);
    }
  }
}

