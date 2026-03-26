using GoFibreSpeedTester.Models;
using GoFibreSpeedTester.Services;
using GoFibreSpeedTester;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GoFibreSpeedTester.Tests;

[TestClass]
public sealed class CsvResultWriterTests
{
  [TestMethod]
  public async Task AppendAsync_WritesHeaderOnce()
  {
    var temp = Path.Combine(Path.GetTempPath(), "GoFibreSpeedTesterTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temp);

    var csvPath = Path.Combine(temp, "out.csv");

    var options = Options.Create(new SpeedTestOptions
    {
      Url = "https://example.invalid",
      OutputCsvPath = csvPath,
      AppendHeaderIfNewFile = true
    });

    var writer = new CsvResultWriter(options, NullLogger<CsvResultWriter>.Instance);

    var r1 = new SpeedTestResult(DateTimeOffset.UtcNow, 1, 2, 3, 4, 5, null, "1", "2", "3", "4", "5");
    var r2 = new SpeedTestResult(DateTimeOffset.UtcNow, 6, 7, 8, 9, 10, null, "6", "7", "8", "9", "10");

    await writer.AppendAsync(r1, CancellationToken.None);
    await writer.AppendAsync(r2, CancellationToken.None);

    var lines = await File.ReadAllLinesAsync(csvPath);
    Assert.IsGreaterThanOrEqualTo(lines.Length, 3);
    Assert.IsTrue(lines[0].Contains("timestampUtc", StringComparison.OrdinalIgnoreCase));
  }
}

