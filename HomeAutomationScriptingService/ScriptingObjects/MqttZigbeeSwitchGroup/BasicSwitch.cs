namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    /// <summary>
    /// Information about Aqara and Tuya switches
    /// </summary>
    /// <param name="Configuration">Configuration with Logger, MemoryCache and MqttClient references</param>
    /// <param name="SwitchButtonStateChanged">Callback to method called when switch-button has changed it state</param>
    /// <param name="MqttPrefix">Zigbee prefix for topic of the switch</param>
    /// <param name="SwitchId">MAC-id of the switch</param>
    /// <param name="SwitchButton">Name of the switch button</param>
    public class BasicSwitch(MqttZigbeeSwitchGroupsConfiguration Configuration,
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

            // kludge solution for absent button-name of the one-button switches...
            var stateButton = SwitchButton == UnnamedButton ? "state" : $"state_{SwitchButton}";

            Configuration.MqttClient!.Publish($"{MqttPrefix}/{SwitchId}/set/{stateButton}", toState);
        }

        /// <summary>
        /// Subscribe to MQTT-topics
        /// </summary>
        protected override void MqttSubscribe()
        {
            Configuration.Logger?.LogTrace("{ClassName} - {MethodName} - {Switch} - {Button}",
                nameof(BasicSwitch), nameof(MqttSubscribe), SwitchId, SwitchButton);

            if (LogErrorIfMqttIsNull())
                return;

            Configuration.MqttClient!.Subscribe($"{@MqttPrefix}/{SwitchId}/action", SwitchGroupStateUpdated);
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

            Configuration.MqttClient!.Unsubscribe(SwitchGroupStateUpdated, $"{@MqttPrefix}/{SwitchId}/action");
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

            // fix one-button switch action payload. Add ephemeral name of button 'main'...
            if (!payload.Contains('_'))
                payload = $"{payload}_{UnnamedButton}";

            var state = payload.Split('_');
            string switchButton = state[1];
            string toState = state[0];

            // we get information from another button
            if (switchButton != SwitchButton)
                return;

            Configuration.Logger?.LogInformation("{Switch} - {Button} state updated. " +
                "Current state: {curState}. To state: {toState}.",
                SwitchId, SwitchButton, SwitchButtonState, toState);

            OnSwitchButtonStateSet(toState);
        }
    }
}
