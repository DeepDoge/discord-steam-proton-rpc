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
            var taskThread = new Thread(() =>
            {
                var didStuff = true;

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

                while (!exit)
                {
                    if (didStuff) Console.WriteLine("Press X to exit");
                    didStuff = false;

                    SetSettings();

                    var steamProcesses = Process.GetProcessesByName("steam.exe").Concat((Process.GetProcessesByName("steam")));
                    Thread.Sleep(1000);
                    foreach (var steamProcess in steamProcesses)
                    {
                        if (steamProcess.MainModule == null) continue;

                        var commandLine = GetCommandLineOfProcess(steamProcess);
                        if (commandLine.Length == 0) continue;
                        var prefix = steamProcess.ProcessName == "steam.exe" ? "c:\\windows\\system32\\steam.exe " : "steam ";
                        if (!commandLine.StartsWith(prefix)) continue;
                        var gameFilename = commandLine.Substring(prefix.Length);
                        if (String.IsNullOrEmpty(gameFilename)) continue;
                        if (!gameFilename.EndsWith(".exe")) continue;

                        var gameDirname = String.Empty;
                        var steamAppsDirname = Path.GetDirectoryName(gameFilename);
                        while (!String.IsNullOrEmpty(steamAppsDirname) && !steamAppsDirname.EndsWith("/steamapps/common"))
                        {
                            gameDirname = steamAppsDirname;
                            steamAppsDirname = Path.GetDirectoryName(gameDirname);
                        }
                        if (steamAppsDirname == String.Empty) continue;

                        var fakeDir = Path.Join(steamAppsDirname, "discord_proton_rpc");
                        var fakeExe = Path.Join(fakeDir, Path.GetFileName(gameDirname));

                        if (IsFileLocked(new FileInfo(fakeExe))) continue;

                        Console.WriteLine($"Found PID:{steamProcess.Id} {gameFilename}");

                        if (!Directory.Exists(fakeDir)) Directory.CreateDirectory(fakeDir);
                        File.Copy(Path.Join(currentDirectory, "rpc-trigger"), fakeExe, true);

                        var rpcProcess = new Process();
                        rpcProcess.StartInfo.FileName = fakeExe;
                        rpcProcess.StartInfo.Arguments = $"{steamProcess.Id} {currentProcess.Id}";
                        rpcProcess.Start();

                        Console.WriteLine($"Running RPC at PID:{rpcProcess.Id} {fakeExe}");
                        didStuff = true;
                    }
                }
            });
            taskThread.Start();

            if (!Console.IsInputRedirected)
            {
                while (true)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.X) break;
                }
                exit = true; // tell thread to exit
            }

            // wait for thread to exit
            taskThread.Join();
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

