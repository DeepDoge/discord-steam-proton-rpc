using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading;
using System.Linq;

namespace MyApp // Note: actual namespace depends on the project name.
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var currentProcess = Process.GetCurrentProcess();

            Console.WriteLine($"Runnning...");

            var exit = false;
            var taskThread = new Thread(() =>
            {
                var didStuff = true;
                while (!exit)
                {
                    if (didStuff) Console.WriteLine("Press X to exit");
                    didStuff = false;

                    var steamProcesses = Process.GetProcessesByName("steam");

                    foreach (var steamProcess in steamProcesses)
                    {
                        if (steamProcess.MainModule == null) continue;

                        var commandLine = GetCommandLineOfProcess(steamProcess);
                        if (commandLine.Length == 0) continue;
                        if (!(commandLine.StartsWith("steam ") && commandLine.EndsWith(".exe"))) continue;
                        var gameFilename = commandLine.Substring("steam ".Length);
                        var gameDirname = "";
                        var steamAppsDirname = Path.GetDirectoryName(gameFilename);
                        while (steamAppsDirname != null && !steamAppsDirname.EndsWith("/steamapps/common"))
                        {
                            gameDirname = steamAppsDirname;
                            steamAppsDirname = Path.GetDirectoryName(gameDirname);
                        }
                        var fakeDir = $"{steamAppsDirname}/discord_proton_rpc";
                        var fakeExe = $"{fakeDir}/{Path.GetFileName(gameDirname)}";

                        if (IsFileLocked(new FileInfo(fakeExe))) continue;

                        Console.WriteLine($"Found PID:{steamProcess.Id} {gameFilename}");

                        if (!Directory.Exists(fakeDir)) Directory.CreateDirectory(fakeDir);
                        File.Copy($"{Path.GetDirectoryName(currentProcess.MainModule.FileName)}/rpc-trigger", fakeExe, true);

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

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.X) break;
            }
            exit = true; // tell thread to exit

            // wait for thread to exit
            while (taskThread.ThreadState == System.Threading.ThreadState.Running) Thread.Sleep(1);
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

