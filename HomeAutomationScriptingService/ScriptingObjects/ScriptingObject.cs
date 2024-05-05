using Microsoft.ClearScript;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService.ScriptingObjects
{
    public interface IScriptingObject
    {
        void InitScriptEngine(IScriptEngine scriptEngine);
    }

    public abstract class ScriptingObject(ILogger<object> logger) : IScriptingObject
    {
        protected ILogger<object> Logger { get; } = logger;
        public abstract void InitScriptEngine(IScriptEngine scriptEngine);
    }

    public abstract class ScriptingConfigurableObject<T>(ILogger<object> logger, IOptions<T> configuration)
        : ScriptingObject(logger) where T : class
    {
        protected T Configuration { get; } = configuration.Value;
    }
}
