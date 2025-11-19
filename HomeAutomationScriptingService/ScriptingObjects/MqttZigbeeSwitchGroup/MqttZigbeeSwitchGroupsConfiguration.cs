using HomeAutomationScriptingService.ScriptingObjects.MqttClient;
using Microsoft.Extensions.Caching.Memory;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    public class MqttZigbeeSwitchGroupsConfiguration
    {
        public required int StateCacheDurationInSeconds { get; set; } = 2;
        public IMemoryCache? MemoryCache { get; set; }
        public required ILogger Logger { get; set; }
        public IScriptingMqttClient? MqttClient { get; set; }

    }
}
