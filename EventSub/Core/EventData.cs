﻿using CorpseLib.DataNotation;
using TwitchCorpse.API;

namespace TwitchCorpse.EventSub.Core
{
    internal class EventData(DataObject data)
    {
        private readonly DataObject m_Data = data;

        public TwitchUser? GetUser(string user = "")
        {
            if (m_Data.TryGet($"{user}user_id", out string? id) &&
                m_Data.TryGet($"{user}user_login", out string? login) &&
                m_Data.TryGet($"{user}user_name", out string? name))
                return new(id!, login!, name!, string.Empty, TwitchUser.Type.NONE, []);
            return null;
        }

        public T GetOrDefault<T>(string key, T defaultValue) => m_Data.GetOrDefault(key, defaultValue)!;

        public bool TryGet<T>(string key, out T? ret) => m_Data.TryGet(key, out ret);
        public List<T> GetList<T>(string key) => m_Data.GetList<T>(key);

        public T? Cast<T>()
        {
            if (DataHelper.Cast(m_Data, out T? ret))
                return ret;
            return default;
        }
    }
}
