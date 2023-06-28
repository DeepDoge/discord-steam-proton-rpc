using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Program
{
    public static void Main(string[] args)
    {
        var currentProcess = Process.GetCurrentProcess();
        if (currentProcess.MainModule == null) throw new Exception("Current process doesn't have a main module\nThis is not expected.");
        var currentDirectory = Path.GetDirectoryName(currentProcess.MainModule.FileName);
        var settingsJsonPath = Path.Join(currentDirectory, "settings.json");
        var settings = default(Settings);
        UpdateSettings(settingsJsonPath, ref settings);

        Console.WriteLine($"Runnning...");

        var exit = false;

        void task()
        {
            Console.WriteLine("Press X to exit");
        top:
            Thread.Sleep(1000);
            try
            {
                UpdateSettings(settingsJsonPath, ref settings);

                var steamProcesses = FindSteamProccesses(ref settings);
                foreach (var steamProcess in steamProcesses)
                {
                    if (steamProcess.MainModule == null) continue;

                    var commandLine = GetCommandLineOfProcess(steamProcess);
                    if (commandLine.Length == 0) continue;

                    var gameFilename = FindGameFilename(commandLine, steamProcess);
                    if (gameFilename == null) continue;

                    var gameDirname = String.Empty;
                    var steamAppsDirname = Path.GetDirectoryName(gameFilename);
                    while (!String.IsNullOrEmpty(steamAppsDirname) && !steamAppsDirname.EndsWith("/steamapps/common"))
                    {
                        gameDirname = steamAppsDirname;
                        steamAppsDirname = Path.GetDirectoryName(gameDirname);
                    }
                    if (String.IsNullOrEmpty(steamAppsDirname)) continue;


                    var gameName = Path.GetFileNameWithoutExtension(gameDirname);
                    if (gameName == "__discord_proton_rpc") continue;

                    var isProtonWineProcess = gameName.StartsWith("SteamLinuxRuntime") || steamProcess.ProcessName == "wineserver";

                    Console.WriteLine($"==================================");
                    Console.WriteLine($"Process commandline: {commandLine}");
                    Console.WriteLine($"Process filename: {gameFilename}");
                    Console.WriteLine($"Is {nameof(isProtonWineProcess)}: {isProtonWineProcess}");

                    if (isProtonWineProcess && !IsSymbolic(gameDirname))
                    {
                        var steamAppsHiddenDirname = Path.Join(Path.GetDirectoryName(steamAppsDirname), "__common_hidden__discord-steam-proton-rpc");
                        if (!Directory.Exists(steamAppsHiddenDirname)) Directory.CreateDirectory(steamAppsHiddenDirname);
                        var gameDirnameHidden = Path.Join(steamAppsHiddenDirname, Path.GetFileName(gameDirname));
                        Console.WriteLine($"Moving {gameDirname} to {gameDirnameHidden}\nAnd linking it back to {gameDirname}");
                        Directory.Move(gameDirname, gameDirnameHidden);
                        Directory.CreateSymbolicLink(gameDirname, gameDirnameHidden);
                    }

                    // Don't do rpc related stuff because its not a game
                    if (isProtonWineProcess) continue;

                    var fakeDir = Path.Join(steamAppsDirname, "__discord_proton_rpc");
                    var fakeExe = Path.Join(fakeDir, gameName);

                    if (File.Exists(fakeExe) && IsFileLocked(new FileInfo(fakeExe))) continue;

                    Console.WriteLine($"Found PID:{steamProcess.Id} PNAME:{steamProcess.ProcessName}");

                    if (!Directory.Exists(fakeDir)) Directory.CreateDirectory(fakeDir);
                    File.Copy(Path.Join(currentDirectory, "rpc-trigger"), fakeExe, true);

                    Thread.Sleep(5000);

                    var rpcProcess = new Process();
                    rpcProcess.StartInfo.FileName = fakeExe;
                    rpcProcess.StartInfo.Arguments = $"{steamProcess.Id} {currentProcess.Id}";
                    rpcProcess.Start();

                    Console.WriteLine($"Running RPC at PID:{rpcProcess.Id} {fakeExe}");
                }
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            if (!exit) goto top;
        }

        new Thread(() =>
        {
            if (!Console.IsInputRedirected)
            {
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.X) break;
                }
                exit = true; // tell thread to exit
            }
        }).Start();

        // Start task
        // Wait for it to exit
        // If task fails for any reason start it again
        while (true)
        {
            var taskThread = new Thread(task);
            taskThread.Start();
            taskThread.Join();
            if (exit) break;
            Console.WriteLine("Restarting Task...");
            Thread.Sleep(2000);
        }
    }

    private static string? FindGameFilename(string commandLine, Process steamProcess)
    {
        var gameFilename = default(string);
        switch (steamProcess.ProcessName)
        {
            case "steam":
            case "steam.exe":
                {
                    var prefix = steamProcess.ProcessName == "steam.exe" ? "c:\\windows\\system32\\steam.exe " : "steam ";
                    if (!commandLine.StartsWith(prefix)) return null;

                    const string gameFilenameSuffix = ".exe";
                    var gameFileNameEnd = commandLine.LastIndexOf(gameFilenameSuffix);
                    if (gameFileNameEnd < 0) return null;
                    gameFileNameEnd += gameFilenameSuffix.Length;

                    var length = gameFileNameEnd - prefix.Length;
                    if (length < 0) throw new Exception($"This should never happen. {nameof(length)} < 0");
                    gameFilename = commandLine.Substring(prefix.Length, length);
                    if (String.IsNullOrEmpty(gameFilename)) return null;
                    if (!gameFilename.EndsWith(".exe")) return null;
                }
                break;
            case "reaper":
                {
                    const string gameFilenamePrefix = "-- /";
                    var startsAt = commandLine.IndexOf(gameFilenamePrefix, commandLine.IndexOf("reaper SteamLaunch AppId="));
                    if (startsAt < 0) return null;
                    gameFilename = commandLine.Substring(startsAt + gameFilenamePrefix.Length - 1);
                    if (String.IsNullOrEmpty(gameFilename)) return null;
                    if (gameFilename.IndexOf(" /home/") >= 0) return null;
                }
                break;
            default:
                if (steamProcess.MainModule == null) return null;
                gameFilename = steamProcess.MainModule.FileName;
                break;
        }

        return gameFilename;
    }

    private static IEnumerable<Process> FindSteamProccesses(ref Settings settings)
    {
        var steamProcesses = Process.GetProcessesByName("steam.exe").Concat(Process.GetProcessesByName("steam"));
        if (settings.detectNonProtonProcesses)
            steamProcesses = steamProcesses.Concat(Process.GetProcessesByName("reaper"));
        if (settings.hideProtonWineProcessesFromDiscordUsingSymbolicLinks)
            steamProcesses = steamProcesses
                .Concat(Process.GetProcessesByName("proton"))
                .Concat(Process.GetProcessesByName("wineserver"))
                .Concat(Process.GetProcessesByName("pressure-vessel-wrap"))
                .Concat(Process.GetProcessesByName("pv-bwrap"));

        return steamProcesses;
    }

    private static void UpdateSettings(string jsonPath, ref Settings settings)
    {
        var defaultSettings = new Settings();
        if (!File.Exists(jsonPath))
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
        var jObjectOfCurrentSettings = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(jsonPath));

        var changesMade = false;

        if (jObjectOfCurrentSettings == null)
        {
            settings = defaultSettings;
            changesMade = true;
        }
        else
        {
            var fields = typeof(Settings).GetFields();
            foreach (var field in fields)
            {
                if (jObjectOfCurrentSettings.ContainsKey(field.Name)) continue;

                var defaultValue = field.GetValue(defaultSettings);
                if (defaultValue == null) throw new Exception();
                jObjectOfCurrentSettings.Add(field.Name, JToken.FromObject(defaultValue));
                changesMade = true;
            }
            settings = jObjectOfCurrentSettings.ToObject<Settings>();
        }

        if (changesMade) File.WriteAllText(jsonPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
    }

    private static bool IsSymbolic(string path)
    {
        FileInfo pathInfo = new FileInfo(path);
        return pathInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private static string GetCommandLineOfProcess(Process process)
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

    private static bool IsFileLocked(FileInfo file)
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

