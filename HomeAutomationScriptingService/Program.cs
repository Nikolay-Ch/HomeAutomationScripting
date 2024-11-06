using HomeAutomationScriptingService.ScriptingObjects;
using HomeAutomationScriptingService.ScriptingObjects.MqttClient;
using HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Reflection;

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

                logger?.LogInformation("Starting the app... Version: {version}", version);

                logger?.LogInformation("Main: Load config done...");

                logger?.LogInformation("Main: Waiting for RunAsync to complete");

                await host.StartAsync();

                await host.WaitForShutdownAsync();

                logger?.LogInformation("Main: RunAsync has completed");
            }
            finally
            {
                Console.WriteLine($"Main: stopping...");
                logger?.LogInformation("Main: stopping...");

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
                .ConfigureLogging((context, loggingBuilder) =>
                {
                    // Adding the OtlpExporter creates a GrpcChannel.
                    // This switch must be set before creating a GrpcChannel when calling an insecure gRPC service.
                    // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                    AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

                    loggingBuilder
                        .ClearProviders()
                        .AddOpenTelemetry(options =>
                        {
                            options.IncludeScopes = true;
                            options.ParseStateValues = true;
                            options.IncludeFormattedMessage = true;
                            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(context.Configuration.GetValue<string>("OpenTelemetry:ServiceName")!));
                        });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    InitScriptingObjects(hostContext, services);

                    services.Configure<WorkerConfiguration>(hostContext.Configuration.GetSection("MainConfiguration"));
                    services.AddMemoryCache();
                    services.AddHostedService<Worker>();
                    services.AddOpenTelemetry()
                        .WithMetrics(builder => builder
                            .AddMeter(ScriptingServiceMetrics.MeterName)
                            .AddRuntimeInstrumentation()
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(hostContext.Configuration.GetValue<string>("OpenTelemetry:ServiceName")!))
                            .AddConsoleExporter()
                        )
                        .UseOtlpExporter(OtlpExportProtocol.HttpProtobuf, new Uri(hostContext.Configuration.GetValue<string>("OpenTelemetry:Endpoint")!));
                });

        private static void InitScriptingObjects(HostBuilderContext hostContext, IServiceCollection services)
        {
            var mqttConfig = hostContext.Configuration.GetSection("MqttConfiguration");
            if (mqttConfig != null)
            {
                services.Configure<MqttClientConfiguration>(mqttConfig);
                services.AddSingleton(typeof(IScriptingObject), typeof(MqttClient));
                services.AddSingleton(typeof(IScriptingObject), typeof(MqttZigbeeSwitchGroups));
                services.AddSingleton<ScriptingServiceMetrics>();
            }
        }
    }
}
