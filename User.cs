namespace TwitchCorpse
{
    public class User
    {
        public enum Type
        {
            NONE,
            MOD,
            GLOBAL_MOD,
            ADMIN,
            STAFF,
            BROADCASTER,
            SELF
        }

        private readonly string m_ID;
        private readonly string m_Name;
        private readonly string m_DisplayName;
        private readonly Type m_Type;

        public string ID => m_ID;
        public string Name => m_Name;
        public string DisplayName => m_DisplayName;
        public Type UserType => m_Type;

        public User(string id, string name, string displayName, Type type)
        {
            m_ID = id;
            m_Name = name;
            m_DisplayName = displayName;
            m_Type = type;
        }

        public User(string id, string name, Type type)
        {
            m_ID = id;
            m_Name = name;
            m_DisplayName = name;
            m_Type = type;
        }
    }
}
