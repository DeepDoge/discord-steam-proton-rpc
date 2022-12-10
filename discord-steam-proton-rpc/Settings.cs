public struct Settings
{
    public bool detectNonProtonProcesses;
    public bool hideProtonWineProcessesFromDiscordUsingSymbolicLinks;

    public Settings()
    {
        detectNonProtonProcesses = true;
        hideProtonWineProcessesFromDiscordUsingSymbolicLinks = true;
    }
}