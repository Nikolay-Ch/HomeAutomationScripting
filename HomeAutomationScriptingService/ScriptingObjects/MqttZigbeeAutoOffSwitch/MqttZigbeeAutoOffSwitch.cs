using HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup;
using HomeAutomationScriptingService.ScriptingObjects.Switches;
using Microsoft.ClearScript;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Switch = HomeAutomationScriptingService.ScriptingObjects.Switches.Switch;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeAutoOffSwitch
{
    /// <summary>
    /// Add auto-off feature of the switch
    /// </summary>
    public class MqttZigbeeAutoOffSwitch : ScriptingConfigurableObject<MqttZigbeeSwitchGroupsConfiguration>
    {
        /// <summary>
        /// List of switch-buttons in this group
        /// </summary>
        public List<(Switch @switch, int offSeconds)> Switches { get; } = [];

        /// <summary>
        /// Cache Id for AutoOff feature
        /// </summary>
        protected string GetCacheKey(Switch @switch) => $"AutoOff-{@switch.SwitchId}";

        public MqttZigbeeAutoOffSwitch(
            IMemoryCache cache,
            ILogger<object> logger,
            IOptions<MqttZigbeeSwitchGroupsConfiguration> componentConfiguration) : base(logger, componentConfiguration)
        {
            ComponentConfiguration.Logger = logger;
            ComponentConfiguration.MemoryCache = cache;
        }

        /// <summary>
        /// Add this object to script engine.
        /// </summary>
        /// <param name="scriptEngine"></param>
        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                nameof(MqttZigbeeAutoOffSwitch), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MqttAutoOffSwitch", this);
        }

        /// <summary>
        /// Script API-method. Setup auto-off feature of the switch
        /// </summary>
        /// <param name="mqttClient"></param>
        public void RegisterAutoOff(int offSeconds, string switchType, string mqttPrefix, string switchId, string switchButton = Switch.UnnamedButton)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {OffSeconds} - {SwitchType} - {MqttPrefix} - {SwitchId} - {SwitchButton}",
                nameof(MqttZigbeeAutoOffSwitch), nameof(RegisterAutoOff), offSeconds, switchType, mqttPrefix, switchId, switchButton);

            Switches.Add((SwitchFactory.CreateSwitch(
                ComponentConfiguration,
                switchType,
                SwitchChangeState,
                mqttPrefix,
                switchId,
                switchButton), offSeconds));
        }

        private void SwitchChangeState(Switch @switch, string stateTo)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {Switch} - {StateTo}",
                nameof(MqttZigbeeAutoOffSwitch), nameof(SwitchChangeState), @switch, stateTo);

            if (stateTo == @switch.StateOnValue)
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(Switches.Find((e) => e.@switch == @switch).offSeconds))
                .RegisterPostEvictionCallback(SwitchOffCallback);

                ComponentConfiguration.MemoryCache!.Set(GetCacheKey(@switch), 1, cacheEntryOptions);
            }

            // delete from cache, if switch has set to off-state
            else
            {
                ComponentConfiguration.MemoryCache!.Remove(GetCacheKey(@switch));
            }
        }

        /// <summary>
        /// Calling, when cache-item was removed from cache, we need to set state of the switch to off-state
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="reason"></param>
        /// <param name="state"></param>
        private void SwitchOffCallback(object key, object? value, EvictionReason reason, object? state)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {Key} - {Value} - {Reason} - {State}",
                nameof(MqttZigbeeAutoOffSwitch), nameof(SwitchOffCallback), key, value, reason, state);

            Switch @switch = (Switch)value!;

            @switch.NeedToSwitchButtonStateSet(@switch.StateOffValue);
        }
    }
}
