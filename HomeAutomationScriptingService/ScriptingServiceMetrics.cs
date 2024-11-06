using System.Diagnostics.Metrics;

namespace HomeAutomationScriptingService
{
    public class ScriptingServiceMetrics
    {
        public static string MeterName => "HomeAutomationScriptingService";

        public Counter<int> UptimeSeconds { get; }
        public Counter<int> ScriptsFound { get; }
        public Counter<int> ScriptsLoaded { get; }
        public Counter<int> MessagesReceived { get; }
        public Counter<int> MessagesSended { get; }

        public ScriptingServiceMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create(MeterName);
            UptimeSeconds = meter.CreateCounter<int>($"uptime", "seconds");
            ScriptsFound = meter.CreateCounter<int>("scripts.found");
            ScriptsLoaded = meter.CreateCounter<int>("scripts.loaded");
            MessagesReceived = meter.CreateCounter<int>("messages.received");
            MessagesSended = meter.CreateCounter<int>("messages.sended");
        }

        protected DateTime ServiceStarted = DateTime.UtcNow;
        private int lastUptameHertbeat = 0;
        private readonly object syncObject = new();
        public void RenewUptime()
        {
            lock (syncObject)
            {
                var currentUptimeInSeconds = (int)(DateTime.UtcNow - ServiceStarted).TotalSeconds;

                UptimeSeconds.Add(currentUptimeInSeconds - lastUptameHertbeat);
                lastUptameHertbeat = currentUptimeInSeconds;
            }
        }
    }
}
