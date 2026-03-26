using GoFibreSpeedTester;
using GoFibreSpeedTester.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Load config from the app's output directory first (where appsettings.json is copied),
// then allow overriding via current working directory and environment variables.
var baseDirSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

builder.Configuration
  .AddJsonFile(baseDirSettings, optional: true, reloadOnChange: true)
  .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
  .AddEnvironmentVariables(prefix: "GOFIBRE_");

builder.Services
  .AddOptions<SpeedTestOptions>()
  .Bind(builder.Configuration.GetSection(SpeedTestOptions.SectionName))
  .ValidateDataAnnotations()
  .Validate(o => !string.IsNullOrWhiteSpace(o.Url), "SpeedTest:Url must be set.")
  .ValidateOnStart();

builder.Services.AddSingleton<ISpeedTestRunner, PlaywrightSpeedTestRunner>();
builder.Services.AddSingleton<IResultWriter, CsvResultWriter>();
builder.Services.AddHostedService<SpeedTestWorker>();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
  o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
  o.SingleLine = true;
});

await builder.Build().RunAsync();
