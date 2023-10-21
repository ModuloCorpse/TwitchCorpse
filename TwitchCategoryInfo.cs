namespace TwitchCorpse
{
    public class TwitchCategoryInfo
    {
        private readonly string m_ID;
        private readonly string m_Name;
        private readonly string m_ImageURL;

        public string ID => m_ID;
        public string Name => m_Name;
        public string ImageURL => m_ImageURL;

        public TwitchCategoryInfo(string id, string name, string imageURL)
        {
            m_ID = id;
            m_Name = name;
            m_ImageURL = imageURL;
        }
    }
}
