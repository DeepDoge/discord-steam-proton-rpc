using System.Diagnostics;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Linq;

namespace MyApp // Note: actual namespace depends on the project name.
{
    public class Program
    {
        private static Dictionary<string, Process> Running = new Dictionary<string, Process>();

        public static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                var watchProccessId = int.Parse(args[0]);
                var processToWatch = Process.GetProcessById(watchProccessId);
                Console.WriteLine($"Watching PID:{watchProccessId}...");
                processToWatch.WaitForExit();
                Console.WriteLine($"Stopping watch process for PID:{watchProccessId}");
                return;
            }

            Console.WriteLine("Runnning...");

            var currentProcess = Process.GetCurrentProcess();

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
                        if (!(commandLine.StartsWith("steam ") && commandLine.ToLower().EndsWith(".exe"))) continue;
                        var filename = commandLine.Substring("steam ".Length);
                        var dirname = Path.GetDirectoryName(filename);
                        if (Running.ContainsKey(dirname)) continue;

                        Console.WriteLine($"Found PID:{steamProcess.Id} {filename}");

                        var fakeExe = $"{dirname}/discord_proton_rpc.fake.exe";
                        if (!File.Exists(fakeExe))
                        {
                            File.CreateSymbolicLink(fakeExe, currentProcess.MainModule.FileName);
                        }

                        var rpcProcess = new Process();
                        rpcProcess.StartInfo.FileName = fakeExe;
                        rpcProcess.StartInfo.Arguments = steamProcess.Id.ToString();
                        rpcProcess.StartInfo.UseShellExecute = false;
                        rpcProcess.Start();

                        Running[dirname] = rpcProcess;

                        // This didnt work for some reasons???
                        // rpcProcess.Exited += new EventHandler((object o, EventArgs a) => Running.Remove(dirname));
                        new Thread(() => {
                            rpcProcess.WaitForExit();
                            Running.Remove(dirname);
                        }).Start();

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

            foreach (var process in Running.Values)
            {
                process.Kill();
            }
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
    }
}

