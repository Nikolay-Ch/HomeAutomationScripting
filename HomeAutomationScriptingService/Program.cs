using System.Reflection;
using HomeAutomationScriptingService.ScriptingObjects;
using Syslog.Framework.Logging;

namespace HomeAutomationScriptingService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";

            Console.WriteLine($"Main: starting. Version: {version}");
            IHost? host = null;
            ILogger<Program>? logger = null;
            try
            {
                host = CreateHostBuilder(args).Build();

                logger = host.Services.GetRequiredService<ILogger<Program>>();

                logger?.LogInformation("Main: Load config done...");

                logger?.LogInformation("Main: Waiting for RunAsync to complete");

                await host.StartAsync();

                await host.WaitForShutdownAsync();

                logger?.LogInformation("Main: RunAsync has completed");
            }
            finally
            {
                logger?.LogInformation("Main: stopping");

                if (host is IAsyncDisposable d) await d.DisposeAsync();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, cfg) =>
                {
                    var env = ctx.HostingEnvironment;

                    cfg.AddJsonFile("appsettings.json", true, false)
                        .AddJsonFile("/config/appsettings.json", true, false)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", true, false)
                        .AddEnvironmentVariables()
                        ;
                })
                .ConfigureLogging((ctx, logging) =>
                {
                    var slConfig = ctx.Configuration.GetSection("SyslogSettings");
                    if (slConfig != null)
                    {
                        var settings = new SyslogLoggerSettings();
                        slConfig.Bind(settings);

                        // Configure structured data here if desired.
                        logging.AddSyslog(settings);
                    }
                })
                .ConfigureServices((hostContext, services) =>
                {
                    InitScriptingObjects(hostContext, services);

                    services.Configure<WorkerConfiguration>(hostContext.Configuration.GetSection("MainConfiguration"));
                    services.AddMemoryCache();
                    services.AddHostedService<Worker>();
                });

        private static void InitScriptingObjects(HostBuilderContext hostContext, IServiceCollection services)
        {
            var mqttConfig = hostContext.Configuration.GetSection("MqttConfiguration");
            if (mqttConfig != null)
            {
                services.Configure<MqttClientConfiguration>(mqttConfig);
                services.AddSingleton(typeof(IScriptingObject), typeof(MqttClient));
                services.AddSingleton(typeof(IScriptingObject), typeof(MqttZigbeeSwitchGroups));
            }
        }
    }
}
