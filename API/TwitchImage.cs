namespace TwitchCorpse
{
    public class TwitchImage
    {
        public class Format(Format.Type type)
        {
            public enum Type
            {
                STATIC,
                ANIMATED
            }

            private readonly Dictionary<float, string> m_URLS = [];
            private readonly Type m_Type = type;

            public Type ThemeType => m_Type;

            public string this[float key]
            {
                get => m_URLS.TryGetValue(key, out string? url) ? url : string.Empty;
                set => m_URLS[key] = value;
            }

            public bool HaveURLs() => m_URLS.Count != 0;
            public bool Have(float scale) => m_URLS.ContainsKey(scale);
        }

        public class Theme
        {
            public enum Type
            {
                DARK,
                LIGHT
            }

            private readonly Format[] m_Formats = new Format[Enum.GetNames(typeof(Format.Type)).Length];
            private readonly Type m_Type;

            public Type ThemeType => m_Type;

            public Format this[Format.Type key]
            {
                get => m_Formats[(int)key];
                set => m_Formats[(int)key] = value;
            }

            public Theme(Type type)
            {
                foreach (Format.Type format in Enum.GetValues(typeof(Format.Type)).Cast<Format.Type>())
                    m_Formats[(int)format] = new(format);
                m_Type = type;
            }
        }

        private readonly Theme[] m_Themes = new Theme[Enum.GetNames(typeof(Theme.Type)).Length];
        private readonly string m_Alt;

        public string Alt => m_Alt;

        public Theme this[Theme.Type key]
        {
            get => m_Themes[(int)key];
            set => m_Themes[(int)key] = value;
        }

        public TwitchImage(string alt)
        {
            foreach (Theme.Type format in Enum.GetValues(typeof(Theme.Type)).Cast<Theme.Type>())
                m_Themes[(int)format] = new(format);
            m_Alt = alt;
        }
    }
}
