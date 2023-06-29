using System.Diagnostics;

public class Program
{
    public static void Main(string[] args)
    {
        var currentProcess = Process.GetCurrentProcess();
        if (currentProcess.MainModule == null) throw new Exception("Current process doesn't have a main module\nThis is not expected.");
        var currentDirectory = Path.GetDirectoryName(currentProcess.MainModule.FileName);
        var settingsJsonPath = Path.Join(currentDirectory, "settings.json");
        var settings = default(Settings);
        settings.Update(settingsJsonPath);

        Console.WriteLine($"Runnning...");

        var exit = false;
        new Thread(() =>
        {
            if (Console.IsInputRedirected) return;
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;
            }
            exit = true;
        }).Start();

        void Loop()
        {
            settings.Update(settingsJsonPath);

            var steamProcesses = Utils.FindSteamProccesses(ref settings);
            foreach (var steamProcess in steamProcesses)
            {
                var gameDirname = String.Empty;
                var steamAppsDirname = Path.GetDirectoryName(steamProcess.filename);
                while (!String.IsNullOrEmpty(steamAppsDirname) && !steamAppsDirname.EndsWith("/steamapps/common"))
                {
                    gameDirname = steamAppsDirname;
                    steamAppsDirname = Path.GetDirectoryName(gameDirname);
                }
                if (String.IsNullOrEmpty(steamAppsDirname)) continue;

                var gameName = Path.GetFileNameWithoutExtension(gameDirname);
                if (gameName == "__discord_proton_rpc") continue;

                if (steamProcess.type == Utils.SteamProcess.Type.ProtonWine)
                {
                    if (Utils.IsSymbolic(gameDirname)) continue;

                    var steamAppsHiddenDirname = Path.Join(Path.GetDirectoryName(steamAppsDirname), "__common_hidden__discord-steam-proton-rpc");
                    if (!Directory.Exists(steamAppsHiddenDirname)) Directory.CreateDirectory(steamAppsHiddenDirname);
                    var gameDirnameHidden = Path.Join(steamAppsHiddenDirname, Path.GetFileName(gameDirname));
                    Console.WriteLine($"Moving {gameDirname} to {gameDirnameHidden}\nAnd linking it back to {gameDirname}");
                    Directory.Move(gameDirname, gameDirnameHidden);
                    Directory.CreateSymbolicLink(gameDirname, gameDirnameHidden);

                    continue;
                }

                var fakeDir = Path.Join(steamAppsDirname, "__discord_proton_rpc");
                var fakeExe = Path.Join(fakeDir, gameName);

                if (File.Exists(fakeExe) && Utils.IsFileLocked(new FileInfo(fakeExe))) continue;

                Console.WriteLine($"Found PID:{steamProcess.process.Id} PNAME:{steamProcess.process.ProcessName}");

                if (!Directory.Exists(fakeDir)) Directory.CreateDirectory(fakeDir);
                File.Copy(Path.Join(currentDirectory, "rpc-trigger"), fakeExe, true);

                Thread.Sleep(5000);

                var rpcProcess = new Process();
                rpcProcess.StartInfo.FileName = fakeExe;
                rpcProcess.StartInfo.Arguments = $"{steamProcess.process.Id} {currentProcess.Id}";
                rpcProcess.Start();

                Console.WriteLine($"Running RPC at PID:{rpcProcess.Id} {fakeExe}");
            }
        }

        while (true)
        {
            var thread = new Thread(() =>
            {
                while (!exit)
                {
                    Thread.Sleep(1000);
                    try
                    {
                        Loop();
                    }
                    catch (System.Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            });
            thread.Start();
            thread.Join();
            if (exit) break;
            Console.WriteLine("Restarting Task...");
            Thread.Sleep(2000);
        }
    }
}

