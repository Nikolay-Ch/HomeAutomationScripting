using HomeAutomationScriptingService.ScriptingObjects.MqttClient;
using Microsoft.ClearScript;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    /// <summary>
    /// Group of the switches and its buttons...
    /// </summary>
    /// <param name="Logger">Logger reference</param>
    /// <param name="SwitchButtonStateCache">Cache reference</param>
    /// <param name="StateCacheDurationInSeconds">Cache duration of the switch-button state</param>
    public class SwitchGroup(MqttZigbeeSwitchGroupsConfiguration Configuration)
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
        public void AddSwitch(string switchType, string mqttPrefix, string switchId, string switchButton = Switch.UnnamedButton)
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {GroupId}, {mqttPrefix}, {switchId}, {switchButton}",
                nameof(SwitchGroup), nameof(AddSwitch), GroupId, mqttPrefix, switchId, switchButton);

            Switches.Add(SwitchFactory.CreateSwitch(Configuration, switchType, ChangeGroupState, mqttPrefix, switchId, switchButton));
        }

        /// <summary>
        /// Lock object to prevent parellel-run of the ChangeSwitchGroupState method
        /// </summary>
        readonly object ChangeSwitchGroupStateSyncObject = new();

        /// <summary>
        /// Change state of the all switch-buttons in switch-button group
        /// </summary>
        /// <param name="stateTo">Target state of the switch-buttons</param>
        protected void ChangeGroupState(Switch switchFrom, string stateTo)
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {GroupId}, {stateTo}",
                nameof(SwitchGroup), nameof(ChangeGroupState), GroupId, stateTo);

            lock (ChangeSwitchGroupStateSyncObject)
                foreach (var @switch in Switches.Where(e => e.SwitchId != switchFrom.SwitchId))
                    @switch.NeedToSwitchButtonStateSet(stateTo);
        }

        /// <summary>
        /// Script API-method. Run switch-button group.
        /// </summary>
        /// <param name="group">Instance of the switch-button group to run</param>
        public void Run()
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {groupId}",
                nameof(SwitchGroup), nameof(Run), GroupId);

            foreach (var @switch in Switches)
                @switch.Run();
        }
    }

    /// <summary>
    /// Scripting object, that used in Scripting service.
    /// It needed to group some switch-buttons in one group to change they state simultaneously
    /// </summary>
    /// <param name="cache">Cache reference</param>
    /// <param name="logger">Logger reference</param>
    /// <param name="componentConfiguration">Configuration reference</param>
    public class MqttZigbeeSwitchGroups : ScriptingConfigurableObject<MqttZigbeeSwitchGroupsConfiguration>
    {
        protected List<SwitchGroup> SwitchGroups { get; } = [];

        public MqttZigbeeSwitchGroups(IMemoryCache cache,
            ILogger<MqttZigbeeSwitchGroups> logger,
            IOptions<MqttZigbeeSwitchGroupsConfiguration> componentConfiguration) : base(logger, componentConfiguration)
        {
            ComponentConfiguration.Logger = Logger;
            ComponentConfiguration.MemoryCache = cache;
        }


        /// <summary>
        /// Add this object to scripting engine.
        /// </summary>
        /// <param name="scriptEngine"></param>
        public override void InitScriptEngine(IScriptEngine scriptEngine)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {scriptEngine}",
                nameof(MqttZigbeeSwitchGroups), nameof(InitScriptEngine), scriptEngine);

            scriptEngine.AddHostObject("MqttGroupSwitch", this);
        }

        /// <summary>
        /// Script API-method. Init this object by MqttClient object
        /// </summary>
        /// <param name="mqttClient"></param>
        public void Init(IMqttClient mqttClient)
        {
            Logger.LogTrace("{ClassName} - {MethodName} - {mqttClient}",
                nameof(MqttZigbeeSwitchGroups), nameof(Init), mqttClient);

            ComponentConfiguration.MqttClient = mqttClient;
        }

        /// <summary>
        /// Script API-method. Create new switch-button group
        /// </summary>
        /// <returns>Instance of the new switch-button group</returns>
        public SwitchGroup RegisterSwitchGroup()
        {
            Logger.LogTrace("{ClassName} - {MethodName}",
                nameof(MqttZigbeeSwitchGroups), nameof(RegisterSwitchGroup));

            var sw = new SwitchGroup(ComponentConfiguration);

            SwitchGroups.Add(sw);

            return sw;
        }
    }
}
