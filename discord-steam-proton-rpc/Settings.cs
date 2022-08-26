namespace MyApp
{
    public struct Settings
    {
        public bool createSymbolicLinkForSteamApps;
        public bool detectEveryGameProcess;

        public Settings()
        {
            createSymbolicLinkForSteamApps = true;
            detectEveryGameProcess = true;
        }
    } 
}