namespace TwitchCorpse.API
{
    public class TwitchImage()
    {
        private readonly Dictionary<float, string> m_URLS = [];

        public string this[float key]
        {
            get => m_URLS.TryGetValue(key, out string? url) ? url : string.Empty;
            set => m_URLS[key] = value;
        }

        public bool HaveURLs() => m_URLS.Count != 0;
        public bool Have(float scale) => m_URLS.ContainsKey(scale);
    }
}
