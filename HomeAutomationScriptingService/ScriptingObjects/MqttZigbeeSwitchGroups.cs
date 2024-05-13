using Microsoft.ClearScript;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService.ScriptingObjects
{
    /// <summary>
    /// Information about switch
    /// </summary>
    /// <param name="MqttPrefix">Zigbee prefix for topic of the switch</param>
    /// <param name="SwitchId">MAC-id of the switch</param>
    /// <param name="SwitchButton">Name of the switch button</param>
    /// <param name="Logger">Logger reference</param>
    /// <param name="SwitchButtonStateCache">Memory cache reference</param>
    /// <param name="SwitchStateDurationInSeconds">Cache duration of the switch-button state</param>
    public readonly record struct Switch(string MqttPrefix, string SwitchId, string SwitchButton,
        ILogger Logger, IMemoryCache SwitchButtonStateCache, int SwitchStateDurationInSeconds)
    {
        // default cache key
        private string CacheKey => $"{SwitchId}_{SwitchButton}";

        /// <summary>
        /// Store constant of the default (unnamed) button for switches, that have single button or have unnamed button
        /// Kludge solution for switches, that have only one button without name (ex. Aqara switch H1)...
        /// you need to use this constant in script
        /// </summary>
        public const string UnnamedButton = "*/main/*";

        /// <summary>
        /// Switch-button state. We use cache to temporary store the button state to reduce coun of change-state zigbee commands
        /// </summary>
        public string? SwitchButtonState
        {
            get
            {
                Logger.LogTrace("{StructName} - {MethodName_Get} - {switchId}, {switchButton}",
                    nameof(Switch), nameof(SwitchButtonState), SwitchId, SwitchButton);

                SwitchButtonStateCache.TryGetValue(CacheKey, out string? state);

                return state;
            }
            set
            {
                Logger.LogTrace("{StructName} - {MethodName_Set} - {switchId}, {switchButton}, {state}",
                    nameof(Switch), nameof(SwitchButtonState), SwitchId, SwitchButton, value);

                SwitchButtonStateCache.Set(
                    $"{SwitchId}_{SwitchButton}",
                    value, TimeSpan.FromSeconds(SwitchStateDurationInSeconds));
            }
        }

        /// <summary>
        /// Used in logs...
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"(Id:{SwitchId}, Btn:{SwitchButton}, State:{SwitchButtonState})";
    }

    /// <summary>
    /// Group of the switches and its buttons...
    /// </summary>
    /// <param name="Logger">Logger reference</param>
    /// <param name="SwitchButtonStateCache">Cache reference</param>
    /// <param name="StateCacheDurationInSeconds">Cache duration of the switch-button state</param>
    public class SwitchGroup(ILogger Logger, IMemoryCache SwitchButtonStateCache, int StateCacheDurationInSeconds)
    {
        /// <summary>
        /// Group Id. Created automatically.
        /// </summary>
        public Guid GroupId { get; } = Guid.NewGuid();

        /// <summary>
        /// List of switch-buttons in this group
        /// </summary>
        public List<Switch> Switches { get; } = [];

        /// <summary>
        /// Script API-method. Add new switch-button in the group
        /// </summary>
        /// <param name="mqttPrefix">Zigbee prefix for topic of the switch</param>
        /// <param name="switchId">MAC-id of the switch</param>
        /// <param name="switchButton">Name of the switch button</param>
        public void AddSwitch(string mqttPrefix, string switchId, string? switchButton = null)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {GroupId}, {mqttPrefix}, {switchId}, {switchButton}",
                nameof(SwitchGroup), nameof(AddSwitch), GroupId, mqttPrefix, switchId, switchButton);

            Switches.Add(new(mqttPrefix, switchId, switchButton ?? Switch.UnnamedButton, Logger, SwitchButtonStateCache, StateCacheDurationInSeconds));

        }
    }

    /// <summary>
    /// Scripting object, that used in Scripting service.
    /// It needed to group some switch-buttons in one group to change they state simultaneously
    /// </summary>
    /// <param name="cache">Cache reference</param>
    /// <param name="logger">Logger reference</param>
    /// <param name="componentConfiguration">Configuration reference</param>
    public class MqttZigbeeSwitchGroups(
            IMemoryCache cache,
            ILogger<MqttZigbeeSwitchGroups> logger,
            IOptions<MqttZigbeeSwitchGroupsConfiguration> componentConfiguration) :
        ScriptingConfigurableObject<MqttZigbeeSwitchGroupsConfiguration>(logger, componentConfiguration)
    {
        protected IMqttClient? MqttClient { get; set; }
        protected List<SwitchGroup> SwitchGroups { get; } = [];
        protected IMemoryCache SwitchButtonStateCache { get; } = cache;

        /// <summary>
        /// Add this object to scripting engine.
        /// </summary>
        /// <param name="scriptEngine"></param>
        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                nameof(MqttZigbeeSwitchGroups), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MqttGroupSwitch", this);
            scriptEngine.AddHostType(typeof(Switch));
        }

        /// <summary>
        /// Script API-method. Init this object by MqttClient object
        /// </summary>
        /// <param name="mqttClient"></param>
        public void Init(IMqttClient mqttClient)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {mqttClient}",
                nameof(MqttZigbeeSwitchGroups), nameof(Init), mqttClient);

            MqttClient = mqttClient;
        }

        /// <summary>
        /// Script API-method. Create new switch-button group
        /// </summary>
        /// <returns>Instance of the new switch-button group</returns>
        public SwitchGroup RegisterSwitchGroup()
        {
            Logger.LogTrace("{ClassName} - {MethodName}", nameof(MqttZigbeeSwitchGroups), nameof(RegisterSwitchGroup));

            var sw = new SwitchGroup(Logger, SwitchButtonStateCache, ComponentConfiguration.StateCacheDurationInSeconds);

            SwitchGroups.Add(sw);

            return sw;
        }

        /// <summary>
        /// Script API-method. Run switch-button group.
        /// </summary>
        /// <param name="group">Instance of the switch-button group to run</param>
        public void RunGroup(SwitchGroup group)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {groupName}",
                nameof(MqttZigbeeSwitchGroups), nameof(RunGroup), group.GroupId);

            if (MqttClient == null)
            {
                Logger.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return;
            }

            foreach (var (MqttPrefix, SwitchId, _, _, _, _) in SwitchGroups.First(e => e.GroupId == group.GroupId).Switches)
                MqttClient.Subscribe($"{MqttPrefix}/{SwitchId}/action", SwitchGroupStateUpdated);
        }

        /// <summary>
        /// This method called by MQTT-broker when MQTT-message arrives
        /// </summary>
        /// <param name="topic">Topic of the MQTT-message</param>
        /// <param name="payload">Payload of the MQTT-message</param>
        private void SwitchGroupStateUpdated(string topic, string payload)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                nameof(MqttZigbeeSwitchGroups), nameof(SwitchGroupStateUpdated), topic, payload);

            // fix one-button switch action payload. Add ephemeral name of button 'main'...
            if (!payload.Contains('_'))
                payload = $"{payload}_{Switch.UnnamedButton}";

            var state = payload.Split('_');
            string switchButton = state[1];
            string toState = state[0];

            // search for group...
            var group = SwitchGroups
                .FirstOrDefault(e => e.Switches.Any(r => topic.Contains(r.SwitchId) && r.SwitchButton == switchButton))
                ?.Switches;

            if (group == null)
                return;

            var @switch = group.First(e => topic.Contains(e.SwitchId));

            @switch.SwitchButtonState =  toState;

            Logger.LogInformation("Switch group: {group}. To state: {toState}. Initiator: {initiator}",
                group, toState, @switch.SwitchId);

            // search for switch button in group
            if (group.Any(e => e.SwitchButton == state[1]))
                ChangeSwitchGroupState(group, state[0]);

            Logger.LogInformation("Switch group: {groupName}. To state: {toState}. Done", group, toState);
        }

        /// <summary>
        /// Lock object to prevent parellel-run of the ChangeSwitchGroupState method
        /// </summary>
        readonly object ChangeSwitchGroupStateSyncObject = new();

        /// <summary>
        /// Change state of the all switch-buttons in switch-button group
        /// </summary>
        /// <param name="switchGroup">List of the switch-buttons to change state</param>
        /// <param name="toState">Target state of the switch-buttons</param>
        private void ChangeSwitchGroupState(List<Switch> switchGroup, string toState)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {switchGroup}, {ToState}",
                nameof(MqttZigbeeSwitchGroups), nameof(ChangeSwitchGroupState), switchGroup, toState);

            lock (ChangeSwitchGroupStateSyncObject)
            {
                // find all switches-button with wrong states
                var switchesNeededToChangeState = switchGroup
                    .Where(e => e.SwitchButtonState != toState)
                    .ToList();

                // set state for each switch-button
                foreach (var sw in switchesNeededToChangeState)
                {
                    Logger.LogInformation("Change switch state. {switch}. To state: {toState}.", sw, toState);

                    SetSwitchButtonState(sw, toState);
                }
            }
        }

        /// <summary>
        /// Set state of the switch-button by sending MQTT-message to zigbee coordinator
        /// </summary>
        /// <param name="switch">Switch reference</param>
        /// <param name="toState">Target state of the switch-button</param>
        private void SetSwitchButtonState(Switch @switch, string toState)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {Switch}, {ToState}",
                nameof(MqttZigbeeSwitchGroups), nameof(SetSwitchButtonState), @switch, toState);

            if (MqttClient == null)
            {
                Logger.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return;
            }

            // kludge solution for absent button-name of the one-button switches...
            var stateButton = @switch.SwitchButton == Switch.UnnamedButton ? "state" : $"state_{@switch.SwitchButton}";

            MqttClient.Publish($"{@switch.MqttPrefix}/{@switch.SwitchId}/set/{stateButton}", toState);
        }
    }
}
