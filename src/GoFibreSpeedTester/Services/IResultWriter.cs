using GoFibreSpeedTester.Models;

namespace GoFibreSpeedTester.Services;

public interface IResultWriter
{
  Task AppendAsync(SpeedTestResult result, CancellationToken cancellationToken);
}

