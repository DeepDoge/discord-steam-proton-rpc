using System.Diagnostics;
public struct SteamProcess
{
    public enum Type
    {
        NativeGame,
        ProtonGame,
        ProtonWineHelper
    }

    public Process process;
    public Type type;
    public string dirname;

    public static HashSet<string> ProtonWineHelperProcessNames = new() {
        "proton",
        "wineserver",
        "pressure-vessel-wrap",
        "pv-bwrap"

    };
    public static readonly string SteamAppNameSuffixInPath = "/steamapps/common/";
    public static IEnumerable<SteamProcess> FindAll(ref Settings settings)
    {
        var steamProcesses = new List<SteamProcess>();
        var foundDirs = new HashSet<string>();

        var allProcesses = Process.GetProcesses();
        foreach (var process in allProcesses)
        {
            var filename = process.MainModule?.FileName ?? "";

            if (filename.Contains(SteamAppNameSuffixInPath))
            {
                var dirname = filename.Substring(0, filename.IndexOf("/", filename.IndexOf(SteamAppNameSuffixInPath) + SteamAppNameSuffixInPath.Length));

                if (foundDirs.Contains(dirname)) continue;
                foundDirs.Add(dirname);

                if (ProtonWineHelperProcessNames.Contains(process.ProcessName))
                {
                    steamProcesses.Add(new()
                    {
                        type = Type.ProtonWineHelper,
                        process = process,
                        dirname = dirname
                    });
                    continue;
                }

                steamProcesses.Add(new()
                {
                    type = Type.NativeGame,
                    process = process,
                    dirname = dirname
                });

                continue;
            }

            if (
                filename.EndsWith("steam/compatibilitytools.d/Proton/bin/wine-preloader") ||
                filename.EndsWith("steam/compatibilitytools.d/Proton/bin/wine64-preloader")
            )
            {

                var commandLine = Utils.GetCommandLineOfProcess(process);
                const string prefix = "Z:\\";
                if (!commandLine.StartsWith(prefix)) continue;
                var gameFilename = $"/{commandLine.Substring(prefix.Length).Replace('\\', '/')}";
                var gameDirname = gameFilename.Substring(0, gameFilename.IndexOf("/", gameFilename.IndexOf(SteamAppNameSuffixInPath) + SteamAppNameSuffixInPath.Length));

                if (foundDirs.Contains(gameDirname)) continue;
                foundDirs.Add(gameDirname);

                steamProcesses.Add(new()
                {
                    type = Type.ProtonGame,
                    process = process,
                    dirname = gameDirname
                });
            }
        }

        return steamProcesses;
    }
}