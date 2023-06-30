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

        Console.WriteLine($"Runnning... (Press ESCAPE to exit)");

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

            var steamProcesses = SteamProcess.FindAll(ref settings);
            Console.WriteLine($"Found {steamProcesses.Count()} Steam processes");
            foreach (var steamProcess in steamProcesses)
            {
                Console.WriteLine($"[{steamProcess.type}] PID:{steamProcess.process.Id} {steamProcess.dirname}");
                if (steamProcess.type == SteamProcess.Type.ProtonWineHelper) continue;

                const string fakeGameDirnameSuffix = "__discord_proton_rpc";
                if (steamProcess.dirname.EndsWith(fakeGameDirnameSuffix)) continue;
                var steamAppsCommonDirname = Path.GetDirectoryName(steamProcess.dirname);
                var steamAppsDirname = Path.GetDirectoryName(steamAppsCommonDirname);

                var gameName = Path.GetFileNameWithoutExtension(steamProcess.dirname);

                var fakeGameDirname = Path.Join(steamAppsCommonDirname, fakeGameDirnameSuffix);
                var fakeGameFilename = Path.Join(fakeGameDirname, gameName);

                if (!Directory.Exists(fakeGameDirname)) Directory.CreateDirectory(fakeGameDirname);
                if (File.Exists(fakeGameFilename) && Utils.IsFileLocked(new FileInfo(fakeGameFilename))) continue;

                File.Copy(Path.Join(currentDirectory, "rpc-trigger"), fakeGameFilename, true);

                var rpcProcess = new Process();
                rpcProcess.StartInfo.FileName = fakeGameFilename;
                rpcProcess.StartInfo.Arguments = $"{steamProcess.process.Id} {currentProcess.Id}";
                rpcProcess.Start();

                Console.WriteLine($"Running RPC at PID:{rpcProcess.Id} {fakeGameFilename}");
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

