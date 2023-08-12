namespace TwitchCorpse
{
    public class ChannelInfo
    {
        private readonly User m_Broadcaster;
        private readonly string m_GameID;
        private readonly string m_GameName;
        private readonly string m_Title;
        private readonly string m_BroadcasterLanguage;

        public ChannelInfo(User broadcaster, string gameID, string gameName, string title, string language)
        {
            m_Broadcaster = broadcaster;
            m_GameID = gameID;
            m_GameName = gameName;
            m_Title = title;
            m_BroadcasterLanguage = language;
        }

        public User Broadcaster => m_Broadcaster;
        public string GameID => m_GameID;
        public string GameName => m_GameName;
        public string Title => m_Title;
        public string BroadcasterLanguage => m_BroadcasterLanguage;
    }
}
