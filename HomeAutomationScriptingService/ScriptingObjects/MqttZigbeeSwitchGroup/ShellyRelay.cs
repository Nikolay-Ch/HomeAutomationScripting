using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    /// <summary>
    /// Information about switch
    /// </summary>
    /// <param name="MqttPrefix">Zigbee prefix for topic of the switch</param>
    /// <param name="SwitchButtonStateChanged">Callback to method called when switch-button has changed it state</param>
    /// <param name="SwitchId">MAC-id of the switch</param>
    /// <param name="SwitchButton">Name of the switch button</param>
    /// <param name="Logger">Logger reference</param>
    /// <param name="SwitchButtonStateCache">Memory cache reference</param>
    /// <param name="SwitchStateDurationInSeconds">Cache duration of the switch-button state</param>
    public class ShellyRelay(MqttZigbeeSwitchGroupsConfiguration Configuration,
        Action<Switch, string>? SwitchButtonStateChanged, string MqttPrefix, string SwitchId, string SwitchButton)
        : Switch(Configuration, SwitchButtonStateChanged, MqttPrefix, SwitchId, SwitchButton)
    {
        /// <summary>
        /// Set state of the switch-button through MQTT-message
        /// </summary>
        /// <param name="toState">Target state</param>
        protected override void MqttSetState(string toState)
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {Switch} - {Button}, {ToState}",
                nameof(BasicSwitch), nameof(MqttSetState), SwitchId, SwitchButton, toState);

            if (LogErrorIfMqttIsNull())
                return;

            Configuration.MqttClient!.Publish($"{MqttPrefix}/{SwitchId}/command/{SwitchButton}", toState);
        }

        /// <summary>
        /// Shelly topic is like this: "shellies/shellydeviceid/status/switch:0"
        /// </summary>
        private string SubscribedMqttTopic => $"{MqttPrefix}/{SwitchId}/status/{SwitchButton}";

        /// <summary>
        /// Subscribe to MQTT-topics
        /// </summary>
        protected override void MqttSubscribe()
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {Switch} - {Button}",
                nameof(BasicSwitch), nameof(MqttSubscribe), SwitchId, SwitchButton);

            if (LogErrorIfMqttIsNull())
                return;

            Configuration.MqttClient!.Subscribe(SubscribedMqttTopic, SwitchGroupStateUpdated);
        }

        /// <summary>
        /// Unsubscribe from MQTT-topics
        /// </summary>
        protected override void MqttUnsubscribe()
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {Switch} - {Button}",
                nameof(BasicSwitch), nameof(MqttUnsubscribe), SwitchId, SwitchButton);

            if (LogErrorIfMqttIsNull())
                return;

            Configuration.MqttClient!.Unsubscribe(SubscribedMqttTopic, SwitchGroupStateUpdated);
        }

        /// <summary>
        /// Callback called when switch change it state and send MQTT-message to MQTT-broker to notify all about state change
        /// </summary>
        /// <param name="topic">MQTT-topic</param>
        /// <param name="payload">MQTT-payload </param>
        private void SwitchGroupStateUpdated(string topic, string payload)
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {topic}, {payload}",
                nameof(BasicSwitch), nameof(SwitchGroupStateUpdated), topic, payload);

            var payloadObj = JsonSerializer.Deserialize<JsonObject>(payload);
            if (payloadObj == null || !payloadObj.TryGetPropertyValue("output", out var toStateObj))
                return;

            var toState = toStateObj!.GetValue<bool>() ? "on" : "off" ;

            Configuration.Logger?.LogInformation("{Switch} - {Button} state updated. " +
                "Current state: {curState}. To state: {toState}.",
                SwitchId, SwitchButton, SwitchButtonState, toState);

            OnSwitchButtonStateSet(toState);
        }
    }
}
