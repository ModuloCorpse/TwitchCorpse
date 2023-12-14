namespace TwitchCorpse
{
    public class TwitchEmoteInfo(TwitchImage image, string id, string name, string emoteType)
    {
        private readonly TwitchImage m_Image = image;
        private readonly string m_ID = id;
        private readonly string m_Name = name;
        private readonly string m_EmoteType = emoteType;

        public TwitchImage Image => m_Image;
        public string ID => m_ID;
        public string Name => m_Name;
        public string EmoteType => m_EmoteType;
    }
}
