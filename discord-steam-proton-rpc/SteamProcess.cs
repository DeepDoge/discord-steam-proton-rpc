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
        "pv-bwrap",
        "wine64",
        "wine",
        "steam.exe",
        "steam"
    };
    public static readonly string SteamAppsCommonPathPart = "/steamapps/common/";
    public static IEnumerable<SteamProcess> FindAll(ref Settings settings)
    {
        var steamProcesses = new List<SteamProcess>();
        var foundDirs = new HashSet<string>();

        var allProcesses = Process.GetProcesses();
        foreach (var process in allProcesses)
        {
            var filename = process.MainModule?.FileName ?? "";

            if (
                (
                    filename.EndsWith("/bin/wine-preloader")
                    || filename.EndsWith("/bin/wine64-preloader")
                )
                && filename.Contains("Proton")
                && filename.ToLower().Contains("/steam/")
            )
            {
                var commandLine = Utils.GetCommandLineOfProcess(process);
                const string prefix = "Z:\\";
                if (!commandLine.StartsWith(prefix)) continue;
                var gameFilename = $"/{commandLine.Substring(prefix.Length).Replace('\\', '/')}";
                var gameDirname = gameFilename.Substring(0, gameFilename.IndexOf("/", gameFilename.IndexOf(SteamAppsCommonPathPart) + SteamAppsCommonPathPart.Length));

                if (foundDirs.Contains(gameDirname)) continue;
                foundDirs.Add(gameDirname);

                steamProcesses.Add(new()
                {
                    type = Type.ProtonGame,
                    process = process,
                    dirname = gameDirname
                });
            }
            else if (filename.Contains(SteamAppsCommonPathPart))
            {
                var dirname = filename.Substring(0, filename.IndexOf("/", filename.IndexOf(SteamAppsCommonPathPart) + SteamAppsCommonPathPart.Length));

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
            }
        }

        return steamProcesses;
    }
}