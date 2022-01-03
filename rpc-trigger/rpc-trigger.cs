using Gtk;
using System.Diagnostics;

class RpcTrigger
{
    static void Main(string[] args)
    {
        Application.Init();

        var exit = false;

        Window window = new Window(Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName));
        window.DeleteEvent += delegate { exit = true; };
        window.DefaultSize = new Gdk.Size(0, 0);
        window.TypeHint = Gdk.WindowTypeHint.Splashscreen;
        window.Show();

        new Thread(() =>
        {
            try
            {
                var procceses = (from arg in args select Process.GetProcessById(int.Parse(arg))).ToArray();
                while (!exit)
                {
                    foreach (var process in procceses)
                    {
                        if (process.HasExited) goto end;
                    }
                    Thread.Sleep(1000);
                    continue;

                end:
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            finally
            {
                Application.Quit();
            }
        }).Start();


        Application.Run();
        Console.WriteLine($"Stopping PID:{Process.GetCurrentProcess().Id}");
    }
}