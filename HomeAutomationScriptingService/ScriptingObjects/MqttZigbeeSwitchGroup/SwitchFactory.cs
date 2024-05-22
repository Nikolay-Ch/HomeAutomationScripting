namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    public class SwitchFactory
    {
        public static Switch CreateSwitch(
            MqttZigbeeSwitchGroupsConfiguration configuration,
            string switchType,
            Action<Switch, string> changeGroupState,
            string mqttPrefix,
            string switchId,
            string switchButton) => switchType switch
            {
                "Aqara" or "Tuya" => new BasicSwitch(configuration, changeGroupState, mqttPrefix, switchId, switchButton),
                "Shelly" => new ShellyRelay(configuration, changeGroupState, mqttPrefix, switchId, switchButton),
                _ => throw new ArgumentException("Invalid argument value", nameof(switchType)),
            };
    }
}
