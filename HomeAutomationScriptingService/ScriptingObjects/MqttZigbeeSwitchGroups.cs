using Microsoft.ClearScript;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService.ScriptingObjects
{
    public readonly record struct Switch(string MqttPrefix, string SwitchId, string SwitchButton, Func<string, string, string?> GetState)
    {
        public string MqttPrefix { get; } = MqttPrefix;
        public string SwitchId { get; } = SwitchId;
        public string SwitchButton { get; } = SwitchButton;
        public string SwitchButtonState => GetState(SwitchId, SwitchButton) ?? "(null)";

        public override string ToString() => $"(Id:{SwitchId}, Btn:{SwitchButton}, State:{SwitchButtonState})";
    }

    public class MqttZigbeeSwitchGroups(
            IMemoryCache cache,
            ILogger<MqttZigbeeSwitchGroups> logger,
            IOptions<MqttZigbeeSwitchGroupsConfiguration> componentConfiguration) :
        ScriptingConfigurableObject<MqttZigbeeSwitchGroupsConfiguration>(logger, componentConfiguration)
    {
        protected IMqttClient? MqttClient { get; set; }
        protected List<(string GroupName, List<Switch> Switches)> SwitchGroups { get; } = [];
        protected IMemoryCache SwitchButtonStateCache { get; } = cache;


        // kludge solution for switches, that have only one button without name (ex. Aqara switch H1)...
        // you need to use this constant in script
#pragma warning disable CA1822 // Mark members as static
        public string DefaultButton => "*/main/*"; // this property used in scripts... so, it can't be a static property
#pragma warning restore CA1822 // Mark members as static

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

            SwitchGroups.First(e => e.GroupName == groupName).Switches.Add(new(mqttPrefix, switchId, switchButton, ReadStateFromCache));
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

            foreach (var (MqttPrefix, SwitchId, _, _) in SwitchGroups.First(e => e.GroupName == groupName).Switches)
                MqttClient.Subscribe($"{MqttPrefix}/{SwitchId}/action", SwitchGroupStateUpdated);
        }

        private void SwitchGroupStateUpdated(string topic, string payload)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                nameof(MqttZigbeeSwitchGroups), nameof(SwitchGroupStateUpdated), topic, payload);

            // fix one-button switch action payload. Add ephemeral name of button 'main'...
            if (!payload.Contains('_'))
                payload = $"{payload}_{DefaultButton}";

            var state = payload.Split('_');
            string switchButton = state[1];
            string toState = state[0];

            // search for group...
            var group = SwitchGroups
                .FirstOrDefault(e => e.Switches.Any(r => topic.Contains(r.SwitchId) && r.SwitchButton == switchButton))
                .Switches;

            if (group == null)
                return;

            string switchId = group.First(e => topic.Contains(e.SwitchId)).SwitchId;

            StoreStateInCache(switchId, switchButton, toState);

            Logger.LogInformation("Switch group: {group}. To state: {toState}. Initiator: {initiator}",
                group, toState, switchId);

            // search for switch button in group
            if (group.Any(e => e.SwitchButton == state[1]))
                ChangeSwitchGroupState(group, state[0]);

            Logger.LogInformation("Switch group: {groupName}. To state: {toState}. Done", group, toState);
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
        private void ChangeSwitchGroupState(List<Switch> switchGroup, string toState)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchGroup}, {ToState}",
                nameof(MqttZigbeeSwitchGroups), nameof(ChangeSwitchGroupState), switchGroup, toState);

            lock (ChangeSwitchGroupStateSyncObject)
            {
                // find all switches-button with wrong states
                var switchesNeededToChangeState = switchGroup
                    .Where(e => GetSwitchButtonState(e.SwitchId, e.SwitchButton) != toState)
                    .ToList();

                // set state for each switch-button
                foreach (var sw in switchesNeededToChangeState)
                {
                    Logger.LogInformation("Change switch state. {switch}. To state: {toState}.", sw, toState);

                    SetSwitchButtonState(sw, toState);
                }
            }
        }

        private string? GetSwitchButtonState(string switchId, string switchButton)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchId}, {switchButton}",
                nameof(MqttZigbeeSwitchGroups), nameof(GetSwitchButtonState), switchId, switchButton);

            return ReadStateFromCache(switchId, switchButton);
        }

        private void SetSwitchButtonState(Switch sw, string toState)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {Switch}, {ToState}",
                nameof(MqttZigbeeSwitchGroups), nameof(SetSwitchButtonState), sw, toState);

            if (MqttClient == null)
            {
                Logger.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return;
            }

            // kludge solution for absent button-name of the one-button switches...
            var stateButton = sw.SwitchButton == DefaultButton ? "state" : $"state_{sw.SwitchButton}";

            MqttClient.Publish($"{sw.MqttPrefix}/{sw.SwitchId}/set/{stateButton}", toState);
        }
    }
}
