using System.Diagnostics;

public static class Utils
{
    public struct SteamProcess
    {
        public enum Type
        {
            Game,
            ProtonWine
        }

        public Process process;
        public Type type;
        public string filename;
    }

    public static IEnumerable<SteamProcess> FindSteamProccesses(ref Settings settings)
    {
        var steamProcesses = Process.GetProcessesByName("steam.exe").Concat(Process.GetProcessesByName("steam")).Select((steamProcess) =>
        {
            var commandLine = GetCommandLineOfProcess(steamProcess);
            var filename = default(string);

            var prefix = steamProcess.ProcessName == "steam.exe" ? "c:\\windows\\system32\\steam.exe " : "steam ";
            if (!commandLine.StartsWith(prefix)) return null;

            const string gameFilenameSuffix = ".exe";
            var gameFileNameEnd = commandLine.LastIndexOf(gameFilenameSuffix);
            if (gameFileNameEnd < 0) return null;
            gameFileNameEnd += gameFilenameSuffix.Length;

            var length = gameFileNameEnd - prefix.Length;
            if (length < 0) throw new Exception($"This should never happen. {nameof(length)} < 0");
            filename = commandLine.Substring(prefix.Length, length);
            if (String.IsNullOrEmpty(filename)) return null;
            if (!filename.EndsWith(".exe")) return null;

            SteamProcess? result = new()
            {
                process = steamProcess,
                type = SteamProcess.Type.Game,
                filename = filename
            };

            return result;
        });

        if (settings.detectNonProtonProcesses)
            steamProcesses.Concat(Process.GetProcessesByName("reaper").Select((reaperProcess) =>
            {

                var commandLine = GetCommandLineOfProcess(reaperProcess);
                var filename = default(string);

                const string gameFilenamePrefix = "-- /";
                var startsAt = commandLine.IndexOf(gameFilenamePrefix, commandLine.IndexOf("reaper SteamLaunch AppId="));
                if (startsAt < 0) return null;
                filename = commandLine.Substring(startsAt + gameFilenamePrefix.Length - 1);
                if (String.IsNullOrEmpty(filename)) return null;
                if (filename.IndexOf(" /home/") >= 0) return null;


                SteamProcess? result = new()
                {
                    process = reaperProcess,
                    type = SteamProcess.Type.Game,
                    filename = filename
                };

                return result;
            }));

        if (settings.hideProtonWineProcessesFromDiscordUsingSymbolicLinks)
            steamProcesses = steamProcesses.Concat(Process.GetProcessesByName("proton")
                .Concat(Process.GetProcessesByName("wineserver"))
                .Concat(Process.GetProcessesByName("pressure-vessel-wrap"))
                .Concat(Process.GetProcessesByName("pv-bwrap")).Select((protonWineProcess) =>
                {
                    var filename = protonWineProcess.MainModule?.FileName;
                    if (filename == null) return null;
                    SteamProcess? result = new()
                    {
                        process = protonWineProcess,
                        type = SteamProcess.Type.ProtonWine,
                        filename = filename
                    };
                    return result;
                }));

        return steamProcesses.Where((steamProcess) => steamProcess != null).Select((steamProcess) => steamProcess!.Value);
    }


    public static bool IsSymbolic(string path)
    {
        FileInfo pathInfo = new FileInfo(path);
        return pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    public static string GetCommandLineOfProcess(Process process)
    {
        var p = new Process();

        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.FileName = "/bin/ps";
        p.StartInfo.Arguments = $"-p {process.Id} -o args";
        p.Start();

        var lines = p.StandardOutput.ReadToEnd().Split('\n');
        p.WaitForExit();
        if (lines.Length < 2) throw new ArgumentException();
        return lines[1];
    }

    public static bool IsFileLocked(FileInfo file)
    {
        try
        {
            using (FileStream stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Close();
            }
        }
        catch (IOException)
        {
            //the file is unavailable because it is:
            //still being written to
            //or being processed by another thread
            //or does not exist (has already been processed)
            return true;
        }

        //file is not locked
        return false;
    }
}