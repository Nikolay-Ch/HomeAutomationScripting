using Microsoft.Extensions.Caching.Memory;

namespace HomeAutomationScriptingService.ScriptingObjects.MqttZigbeeSwitchGroup
{
    /// <summary>
    /// Base switch-button class
    /// </summary>
    /// <param name="Configuration">Configuration object reference</param>
    /// <param name="SwitchButtonStateChanged">Callback to method called when switch-button has changed it state</param>
    /// <param name="MqttPrefix">Prefix of the switch MQTT-topics</param>
    /// <param name="SwitchId">Id of the switch</param>
    /// <param name="SwitchButton">Name of the switch's button</param>
    public abstract class Switch(MqttZigbeeSwitchGroupsConfiguration Configuration,
        Action<Switch, string>? SwitchButtonStateChanged,
        string MqttPrefix, string SwitchId, string SwitchButton = Switch.UnnamedButton) : IDisposable
    {
        /// <summary>
        /// Configuration object
        /// </summary>
        public MqttZigbeeSwitchGroupsConfiguration Configuration { get; } = Configuration;

        /// <summary>
        /// Prefix of the switch MQTT-topics
        /// </summary>
        public string MqttPrefix { get; } = MqttPrefix;

        /// <summary>
        /// Id of the switch
        /// </summary>
        public string SwitchId { get; } = SwitchId;

        /// <summary>
        /// Name of the switch's button
        /// </summary>
        public string SwitchButton { get; } = SwitchButton;

        /// <summary>
        /// Callback reference of the method, that called, when state of the switch-button has changed
        /// </summary>
        public Action<Switch, string>? SwitchButtonStateChanged { get; } = SwitchButtonStateChanged;

        /// <summary>
        /// State of the switch-button
        /// </summary>
        public string? SwitchButtonState
        {
            get
            {
                Configuration.Logger?.LogTrace("{StructName} - {MethodName}_Get - {switchId} - {switchButton}",
                    nameof(Switch), nameof(SwitchButtonState), SwitchId, SwitchButton);

                if (Configuration.MemoryCache == null)
                    return null;

                Configuration.MemoryCache.TryGetValue(CacheKey, out string? state);
                return state;
            }
        }

        /// <summary>
        /// Start working of the switch (subscribe to all needed mqtt-topics)
        /// </summary>
        public void Run() => MqttSubscribe();

        /// <summary>
        /// Stop work (unsubscribe from all mqtt-topics)
        /// </summary>
        public void Stop() => MqttUnsubscribe();

        /// <summary>
        /// External method to change state of the switch-button
        /// </summary>
        /// <param name="stateTo"></param>
        public void NeedToSwitchButtonStateSet(string stateTo)
        {
            Configuration.Logger?.LogTrace("{StructName} - {MethodName} - {switchId} - {switchButton}, State: {curState} => {stateTo}.",
                nameof(Switch), nameof(NeedToSwitchButtonStateSet), SwitchId, SwitchButton, SwitchButtonState, stateTo);

            if (SwitchButtonState != stateTo)
                MqttSetState(stateTo);
        }

        /// <summary>
        /// Called when MQTT send information about state change of the switch-button
        /// </summary>
        /// <param name="stateTo">Target state</param>
        protected void OnSwitchButtonStateSet(string stateTo)
        {
            Configuration.Logger?.LogTrace("{StructName} - {MethodName} - {switchId} - {switchButton}. State: {curState} => {state}.",
                nameof(Switch), nameof(OnSwitchButtonStateSet), SwitchId, SwitchButton, SwitchButtonState, stateTo);

            if (SwitchButtonState != stateTo)
            {
                Configuration.MemoryCache?.Set(CacheKey, stateTo, TimeSpan.FromSeconds(Configuration.StateCacheDurationInSeconds));
                SwitchButtonStateChanged?.Invoke(this, stateTo);
            }
        }

        /// <summary>
        /// Subscribe switch to MQTT-topic
        /// </summary>
        protected abstract void MqttSubscribe();

        /// <summary>
        /// Unsibscribe switch from MQTT
        /// </summary>
        protected abstract void MqttUnsubscribe();

        /// <summary>
        /// Change state of the switch-button through MQTT-broker
        /// </summary>
        /// <param name="value">Target state</param>
        protected abstract void MqttSetState(string value);

        /// <summary>
        /// Log error if MQTT not initialized
        /// </summary>
        /// <returns></returns>
        protected bool LogErrorIfMqttIsNull()
        {
            Configuration.Logger?.LogTrace("{StructName} - {MethodName} - {switchId} - {switchButton}",
                nameof(Switch), nameof(LogErrorIfMqttIsNull), SwitchId, SwitchButton);

            if (Configuration.MqttClient == null)
            {
                Configuration.Logger?.LogWarning("MqttClient is null. Maybe you forget to call Init method?");
                return true;
            }

            return false;
        }

        /// <summary>
        /// default cache key
        /// </summary>
        private string CacheKey => $"{SwitchId}_{SwitchButton}";

        /// <summary>
        /// Store constant of the default (unnamed) button for switches, that have single button or have unnamed button
        /// Kludge solution for switches, that have only one button without name (ex. Aqara switch H1)...
        /// you need to use this constant in script
        /// </summary>
        public const string UnnamedButton = "*/main/*";
        private bool disposedValue;

        /// <summary>
        /// Used in logs...
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"(Id:{SwitchId}, Btn:{SwitchButton}, State:{SwitchButtonState})";

        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            Configuration.Logger?.LogTrace("{StructName} - {MethodName} - {switchId} - {switchButton}. Disposing: {disposing}.",
                nameof(Switch), nameof(Dispose), SwitchId, SwitchButton, disposing);

            if (!disposedValue)
            {
                Stop();

                disposedValue = true;
            }
        }

        ~Switch()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
