using GoFibreSpeedTester.Models;

namespace GoFibreSpeedTester.Services;

public interface ISpeedTestRunner
{
  Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken);
}

