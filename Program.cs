using System.Diagnostics;
using RunOnDisplayWakeup;

var pathToContentRoot = Path.GetDirectoryName(Environment.ProcessPath);

var configBuilder = new ConfigurationBuilder()
              .SetBasePath(pathToContentRoot)
              .AddJsonFile("run-on-display-wakeup_appsettings.json", false, false)
              .AddEnvironmentVariables();

var _configuration = configBuilder.Build();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton(_configuration);
        services.AddHostedService<HostedService>();
    })
    .Build();

await host.RunAsync();

namespace RunOnDisplayWakeup
{
    public class HostedService : BackgroundService
    {
        private readonly ILogger _logger;
        private readonly IConfigurationRoot _config;
        private bool _launchOnNextTick;

        public HostedService(ILogger<HostedService> logger, IConfigurationRoot config)
        {
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var executable = _config["Executable"];

            var fileInfo = new FileInfo(executable);

            if (!fileInfo.Exists)
            {
                _logger.LogError("Executable {executable} not found", executable);
                Environment.Exit(1);
            }

            var workingDirectory = fileInfo?.Directory?.FullName;
            var processName = fileInfo?.Name;

            try
            {
                NvAPIWrapper.NVIDIA.Initialize();

                var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var display = NvAPIWrapper.Display.DisplayDevice.GetGDIPrimaryDisplayDevice();

                    if (!display.IsActive)
                    {
                        if (!_launchOnNextTick)
                        {
                            _logger.LogInformation("Display inactive, relaunching {processName} when active again.", processName);
                            _launchOnNextTick = true;
                        }
                    }
                    else
                    {
                        if (_launchOnNextTick)
                        {
                            _logger.LogInformation("Killing current {processName} process.", processName);

                            _launchOnNextTick = false;

                            var pInfo2 = new ProcessStartInfo("taskkill"!)
                            {
                                WorkingDirectory = workingDirectory,
                                Arguments = $"/IM {processName}",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                            };

                            var processTaskKill = Process.Start(pInfo2)!;
                            await processTaskKill.WaitForExitAsync(CancellationToken.None);

                            _logger.LogInformation("Process {processName} terminated.", processName);

                            var pInfo = new ProcessStartInfo(processName!)
                            {
                                WorkingDirectory = workingDirectory,
                                Arguments = "-minimize",
                                UseShellExecute = true
                            };

                            _logger.LogInformation("Launching {executable} from {workingDirectory}.", processName, workingDirectory);
                            Process.Start(pInfo);

                            _logger.LogInformation("Done.");
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex, "{Message}", ex.Message);
                Environment.Exit(1);
            }
            finally
            {
                NvAPIWrapper.NVIDIA.Unload();
            }
        }
    }
}