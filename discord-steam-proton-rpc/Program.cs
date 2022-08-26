using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyApp // Note: actual namespace depends on the project name.
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var currentProcess = Process.GetCurrentProcess();
            if (currentProcess.MainModule == null) throw new Exception("Current process doesn't have a main module\nThis is not expected.");
            var currentDirectory = Path.GetDirectoryName(currentProcess.MainModule.FileName);
            var settingsJsonPath = Path.Join(currentDirectory, "settings.json");

            Console.WriteLine($"Runnning...");

            var exit = false;

            void task()
            {
                var showExitText = true;

                var settings = default(Settings);
                void SetSettings()
                {
                    var defaultSettings = new Settings();
                    if (!File.Exists(settingsJsonPath))
                        File.WriteAllText(settingsJsonPath, JsonConvert.SerializeObject(defaultSettings, Formatting.Indented));
                    var jObjectOfCurrentSettings = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(settingsJsonPath));

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
                            if (!jObjectOfCurrentSettings.ContainsKey(field.Name))
                            {
                                var defaultValue = field.GetValue(defaultSettings);
                                if (defaultValue == null) throw new Exception();
                                jObjectOfCurrentSettings.Add(field.Name, JToken.FromObject(defaultValue));
                                changesMade = true;
                            }
                        }
                        settings = jObjectOfCurrentSettings.ToObject<Settings>();
                    }

                    if (changesMade) File.WriteAllText(settingsJsonPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
                }
                SetSettings();

                while (!exit)
                {
                    try
                    {
                        if (showExitText) Console.WriteLine("Press X to exit");
                        showExitText = false;

                        SetSettings();

                        var steamProcesses = Process.GetProcessesByName("steam.exe").Concat(Process.GetProcessesByName("steam"));
                        if (settings.detectNonProtonProcesses)
                            steamProcesses = steamProcesses.Concat(Process.GetProcessesByName("reaper"));
                        if (settings.hideProtonWineProcessesFromDiscordUsingSymbolicLinks)
                            steamProcesses = steamProcesses
                                .Concat(Process.GetProcessesByName("wineserver"))
                                .Concat(Process.GetProcessesByName("pressure-vessel-wrap"));

                        Thread.Sleep(1000);
                        foreach (var steamProcess in steamProcesses)
                        {
                            if (steamProcess.MainModule == null) continue;

                            var commandLine = GetCommandLineOfProcess(steamProcess);
                            if (commandLine.Length == 0) continue;

                            var gameFilename = default(string);
                            switch (steamProcess.ProcessName)
                            {
                                case "steam":
                                case "steam.exe":
                                    {
                                        var prefix = steamProcess.ProcessName == "steam.exe" ? "c:\\windows\\system32\\steam.exe " : "steam ";
                                        if (!commandLine.StartsWith(prefix)) continue;

                                        const string gameFilenameSuffix = ".exe";
                                        var gameFileNameEnd = commandLine.LastIndexOf(gameFilenameSuffix) + gameFilenameSuffix.Length;
                                        if (gameFileNameEnd < 0) continue;
                                        gameFilename = commandLine.Substring(prefix.Length, gameFileNameEnd - prefix.Length);
                                        if (String.IsNullOrEmpty(gameFilename)) continue;
                                        if (!gameFilename.EndsWith(".exe")) continue;
                                    }
                                    break;
                                case "reaper":
                                    {
                                        const string gameFilenamePrefix = "-- /";
                                        var startsAt = commandLine.IndexOf(gameFilenamePrefix, commandLine.IndexOf("reaper SteamLaunch AppId="));
                                        if (startsAt < 0) continue;
                                        gameFilename = commandLine.Substring(startsAt + gameFilenamePrefix.Length - 1);
                                        if (String.IsNullOrEmpty(gameFilename)) continue;
                                        if (gameFilename.IndexOf(" /home/") >= 0) continue;
                                    }
                                    break;
                                default:
                                    gameFilename = steamProcess.MainModule.FileName;
                                    break;
                            }

                            Console.WriteLine($"==================================");
                            Console.WriteLine($"Process filename: {gameFilename}");
                            Console.WriteLine($"Process commandline: {commandLine}");

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

                            var rpcProcess = new Process();
                            rpcProcess.StartInfo.FileName = fakeExe;
                            rpcProcess.StartInfo.Arguments = $"{steamProcess.Id} {currentProcess.Id}";
                            rpcProcess.Start();

                            Console.WriteLine($"Running RPC at PID:{rpcProcess.Id} {fakeExe}");
                            showExitText = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        File.AppendAllText(Path.Join(currentDirectory, "log.log"), $"{ex.ToString()}\n");
                    }
                }
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
}

