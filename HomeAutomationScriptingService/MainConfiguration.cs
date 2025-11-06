namespace HomeAutomationScriptingService
{
    public record class MainConfiguration
    {
        public required string ScriptsFilesFolder { get; init; } = "scripts";
    }
}
