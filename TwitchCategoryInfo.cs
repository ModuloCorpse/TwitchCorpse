namespace TwitchCorpse
{
    public class TwitchCategoryInfo(string id, string name, string imageURL)
    {
        private readonly string m_ID = id;
        private readonly string m_Name = name;
        private readonly string m_ImageURL = imageURL;
        public string ID => m_ID;
        public string Name => m_Name;
        public string ImageURL => m_ImageURL;
    }
}
