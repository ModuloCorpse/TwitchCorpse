namespace TwitchCorpse.API
{
    public class TwitchChannelInfo(TwitchUser broadcaster, string gameID, string gameName, string title, string language)
    {
        private readonly TwitchUser m_Broadcaster = broadcaster;
        private readonly string m_GameID = gameID;
        private readonly string m_GameName = gameName;
        private readonly string m_Title = title;
        private readonly string m_BroadcasterLanguage = language;
        public TwitchUser Broadcaster => m_Broadcaster;
        public string GameID => m_GameID;
        public string GameName => m_GameName;
        public string Title => m_Title;
        public string BroadcasterLanguage => m_BroadcasterLanguage;
    }
}
