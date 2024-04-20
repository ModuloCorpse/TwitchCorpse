using CorpseLib.DataNotation;

namespace TwitchCorpse.EventSub.Core
{
    internal class EventData(DataObject data)
    {
        private readonly DataObject m_Data = data;

        public TwitchUser? GetUser(string user = "")
        {
            if (m_Data.TryGet(string.Format("{0}user_id", user), out string? id) &&
                m_Data.TryGet(string.Format("{0}user_login", user), out string? login) &&
                m_Data.TryGet(string.Format("{0}user_name", user), out string? name))
                return new(id!, login!, name!, string.Empty, TwitchUser.Type.NONE, []);
            return null;
        }

        public T GetOrDefault<T>(string key, T defaultValue) => m_Data.GetOrDefault(key, defaultValue)!;

        public bool TryGet<T>(string key, out T? ret) => m_Data.TryGet(key, out ret);
        public List<T> GetList<T>(string key) => m_Data.GetList<T>(key);
    }
}
