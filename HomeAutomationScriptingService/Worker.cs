using HomeAutomationScriptingService.ScriptingObjects;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService
{
    public class Worker(
        IEnumerable<IScriptingObject> scriptingObjects,
        IOptions<WorkerConfiguration> workerOptions,
        ILogger<Worker> logger,
        ScriptingServiceMetrics metrics) : BackgroundService
    {
        protected IEnumerable<IScriptingObject> ScriptingObjects { get; } = new List<IScriptingObject>(scriptingObjects);
        protected WorkerConfiguration WorkerOptions { get; } = workerOptions.Value;
        protected ILogger<Worker> Logger { get; } = logger;
        protected ScriptingServiceMetrics Metrics { get; } = metrics;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var engine = new V8ScriptEngine(V8ScriptEngineFlags.AddPerformanceObject);
            foreach (var scriptingObject in scriptingObjects)
                scriptingObject.InitScriptEngine(engine);

            var scripts = Directory.EnumerateFiles("./scripts").ToList();
            Logger.LogInformation("{scriptsCount} scripts founded.", scripts.Count);
            Metrics.ScriptsFound.Add(scripts.Count);

            foreach (var scriptFilePath in scripts)
            {
                try
                {
                    Logger.LogInformation("Try to load script: {scriptFilePath}", scriptFilePath);
                    var scriptText = File.ReadAllText(scriptFilePath);

                    Logger.LogInformation("Try to execute script: {scriptFilePath}", scriptFilePath);
                    engine.Execute(scriptText);

                    Metrics.ScriptsLoaded.Add(1);
                }
                catch { }
            }

            Logger.LogInformation("Loading scripts: done");

            while (!stoppingToken.IsCancellationRequested)
            {
                Metrics.RenewUptime();

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogTrace("Worker running at: {time}", DateTimeOffset.Now);
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
