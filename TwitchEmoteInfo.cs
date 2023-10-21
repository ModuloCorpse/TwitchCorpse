namespace TwitchCorpse
{
    public class TwitchEmoteInfo
    {
        public enum FormatType
        {
            STATIC,
            ANIMATED
        }

        private readonly List<string> m_Scale;
        private readonly List<string> m_ThemeMode;
        private readonly string m_ID;
        private readonly string m_Name;
        private readonly string m_EmoteType;
        private readonly FormatType m_Format;

        public TwitchEmoteInfo(string id, string name, string emoteType, List<string> format, List<string> scale, List<string> themeMode)
        {
            m_ID = id;
            m_Name = name;
            m_EmoteType = emoteType;
            m_Format = FormatType.STATIC;
            foreach (var item in format)
            {
                if (item == "animated")
                    m_Format = FormatType.ANIMATED;
            }
            m_Scale = scale;
            m_ThemeMode = themeMode;
        }

        public string ID => m_ID;
        public string Name => m_Name;
        public string EmoteType => m_EmoteType;
        public FormatType Format => m_Format;
        public List<string> Scale => m_Scale;
        public List<string> ThemeMode => m_ThemeMode;
    }
}
