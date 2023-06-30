using System.Diagnostics;

public static class Utils
{
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