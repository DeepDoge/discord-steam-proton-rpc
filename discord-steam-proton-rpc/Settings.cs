using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public struct Settings
{
    public bool detectNonProtonProcesses;
    public bool hideProtonWineProcessesFromDiscordUsingSymbolicLinks;
}

public static class SettingsImpl
{
    private static readonly Settings DefaultSettings = new Settings
    {
        detectNonProtonProcesses = true,
        hideProtonWineProcessesFromDiscordUsingSymbolicLinks = true
    };

    public static void Update(this ref Settings self, string settingsJsonPath)
    {
        if (!File.Exists(settingsJsonPath)) File.WriteAllText(settingsJsonPath, JsonConvert.SerializeObject(DefaultSettings, Formatting.Indented));
        var jObjectOfCurrentSettings = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(settingsJsonPath));

        var changesMade = false;

        if (jObjectOfCurrentSettings == null)
        {
            self = DefaultSettings;
            changesMade = true;
        }
        else
        {
            var fields = typeof(Settings).GetFields();
            foreach (var field in fields)
            {
                if (jObjectOfCurrentSettings.ContainsKey(field.Name)) continue;

                var defaultValue = field.GetValue(DefaultSettings);
                if (defaultValue == null) throw new Exception();
                jObjectOfCurrentSettings.Add(field.Name, JToken.FromObject(defaultValue));
                changesMade = true;
            }
            self = jObjectOfCurrentSettings.ToObject<Settings>();
        }

        if (changesMade) File.WriteAllText(settingsJsonPath, JsonConvert.SerializeObject(self, Formatting.Indented));
    }
}