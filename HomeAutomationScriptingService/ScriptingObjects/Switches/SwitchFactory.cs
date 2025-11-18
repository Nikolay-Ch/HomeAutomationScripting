using HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup;

namespace HomeAutomationScriptingService.ScriptingObjects.Switches;

public class SwitchFactory
{
    public static Switch CreateSwitch(
        MqttZigbeeSwitchGroupsConfiguration configuration,
        string switchType,
        Action<Switch, string> changeSwitchState,
        string mqttPrefix,
        string switchId,
        string switchButton) => switchType switch
        {
            "Aqara" or "Tuya" => new AqaraTuyaSwitch(configuration, changeSwitchState, mqttPrefix, switchId, switchButton),
            "Shelly" => new ShellyRelay(configuration, changeSwitchState, mqttPrefix, switchId, switchButton),
            _ => throw new ArgumentException("Invalid argument value", nameof(switchType)),
        };
}
