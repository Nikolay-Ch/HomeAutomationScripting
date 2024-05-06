using Microsoft.ClearScript;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService.ScriptingObjects
{
    public class MqttZigbeeSwitchGroups(
            IMemoryCache cache,
            ILogger<MqttZigbeeSwitchGroups> logger,
            IOptions<MqttZigbeeSwitchGroupsConfiguration> componentConfiguration) :
        ScriptingConfigurableObject<MqttZigbeeSwitchGroupsConfiguration>(logger, componentConfiguration)
    {
        protected IMqttClient? MqttClient { get; set; }
        protected List<(string GroupName, List<(string MqttPrefix, string SwitchId, string SwitchButton)> Switches)> SwitchGroups { get; } = [];
        protected IMemoryCache SwitchButtonStateCache { get; } = cache;

        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                nameof(MqttZigbeeSwitchGroups), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MqttGroupSwitch", this);
        }

        public void Init(IMqttClient mqttClient)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {mqttClient}",
                nameof(MqttZigbeeSwitchGroups), nameof(Init), mqttClient);

            MqttClient = mqttClient;
        }

        public void RegisterSwitchGroup(string groupName)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {groupName}",
                nameof(MqttZigbeeSwitchGroups), nameof(RegisterSwitchGroup), groupName);

            SwitchGroups.Add((groupName, []));
        }

        public void AddSwitchIntoGroup(string groupName, string mqttPrefix, string switchId, string switchButton)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {groupName}, {mqttPrefix}, {switchId}, {switchButton}",
                nameof(MqttZigbeeSwitchGroups), nameof(AddSwitchIntoGroup), groupName, mqttPrefix, switchId, switchButton);

            SwitchGroups.First(e=>e.GroupName == groupName).Switches.Add((mqttPrefix, switchId, switchButton));
        }

        public void RunGroup(string groupName)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {groupName}",
                nameof(MqttZigbeeSwitchGroups), nameof(RunGroup), groupName);

            if (MqttClient == null)
            {
                Logger.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return;
            }

            foreach (var (MqttPrefix, SwitchId, _) in SwitchGroups.First(e => e.GroupName == groupName).Switches)
                MqttClient.Subscribe($"{MqttPrefix}/{SwitchId}/action", SwitchGroupStateUpdated);
        }

        private void SwitchGroupStateUpdated(string topic, string payload)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                nameof(MqttZigbeeSwitchGroups), nameof(SwitchGroupStateUpdated), topic, payload);

            // search for group...
            var group = SwitchGroups.FirstOrDefault(e => e.Switches.Any(r => topic.Contains(r.SwitchId))).Switches;
            if (group == null)
                return;

            var state = payload.Split('_');

            StoreStateInCache(group.First(e => topic.Contains(e.SwitchId)).SwitchId, state[1], state[0]);

            // search for switch button in group
            if (group.Any(e => e.SwitchButton == state[1]))
                ChangeSwitchGroupState(group, state[0]);
        }

        private void StoreStateInCache(string switchId, string switchButton, string state)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchId}, {switchButton}, {state}",
                nameof(MqttZigbeeSwitchGroups), nameof(StoreStateInCache), switchId, switchButton, state);

            SwitchButtonStateCache.Set(
                $"{switchId}_{switchButton}",
                state,
                TimeSpan.FromSeconds(ComponentConfiguration.StateCacheDurationInSeconds));
        }

        private string? ReadStateFromCache(string switchId, string switchButton)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchId}, {switchButton}",
                nameof(MqttZigbeeSwitchGroups), nameof(ReadStateFromCache), switchId, switchButton);

            SwitchButtonStateCache.TryGetValue($"{switchId}_{switchButton}", out string? state);

            return state;
        }


        readonly object ChangeSwitchGroupStateSyncObject = new();
        private void ChangeSwitchGroupState(List<(string mqttPrefix, string SwitchId, string SwitchButton)> switchGroup, string state)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchGroup}, {state}",
                nameof(MqttZigbeeSwitchGroups), nameof(ChangeSwitchGroupState), switchGroup, state);

            lock (ChangeSwitchGroupStateSyncObject)
            {
                // find all switches-button with wrong states
                var switchesNeededToChangeState = switchGroup
                    .Where(e => GetSwitchButtonState(e.SwitchId, e.SwitchButton) != state)
                    .ToList();

                // set state for each switch-button
                foreach (var (MqttPrefix, SwitchId, SwitchButton) in switchesNeededToChangeState)
                    SetSwitchButtonState(MqttPrefix, SwitchId, SwitchButton, state);
            }
        }

        private string? GetSwitchButtonState(string switchId, string switchButton)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchId}, {switchButton}",
                nameof(MqttZigbeeSwitchGroups), nameof(GetSwitchButtonState), switchId, switchButton);

            return ReadStateFromCache(switchId, switchButton);
        }

        private void SetSwitchButtonState(string mqttPrefix, string switchId, string switchButton, string state)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchId}, {switchButton}, {state}",
                nameof(MqttZigbeeSwitchGroups), nameof(SetSwitchButtonState), switchId, switchButton, state);

            if (MqttClient == null)
            {
                Logger.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return;
            }

            MqttClient.Publish($"{mqttPrefix}/{switchId}/set/state_{switchButton}", state);
        }
    }
}
