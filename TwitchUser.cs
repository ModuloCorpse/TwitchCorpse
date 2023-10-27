﻿using CorpseLib.Json;
using CorpseLib;

namespace TwitchCorpse
{
    public class TwitchUser
    {
        public class JSerializer : AJSerializer<TwitchUser>
        {
            protected override OperationResult<TwitchUser> Deserialize(JObject reader)
            {
                if (reader.TryGet("id", out string? id) &&
                    reader.TryGet("name", out string? name) &&
                    reader.TryGet("display_name", out string? displayName) &&
                    reader.TryGet("user_type", out Type? userType))
                    return new(new(id!, name!, displayName!, (Type)userType!, reader.GetList<TwitchBadgeInfo>("badges")));
                return new("Bad json", string.Empty);
            }

            protected override void Serialize(TwitchUser obj, JObject writer)
            {
                writer["id"] = obj.m_ID;
                writer["name"] = obj.m_Name;
                writer["display_name"] = obj.m_DisplayName;
                writer["user_type"] = obj.m_UserType;
                writer["badges"] = obj.m_Badges;
            }
        }

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

        private readonly List<TwitchBadgeInfo> m_Badges;
        private readonly string m_ID;
        private readonly string m_Name;
        private readonly string m_DisplayName;
        private readonly Type m_UserType;

        public TwitchBadgeInfo[] Badges => m_Badges.ToArray();
        public string ID => m_ID;
        public string Name => m_Name;
        public string DisplayName => m_DisplayName;
        public Type UserType => m_UserType;

        public TwitchUser(string id, string name, string displayName, Type userType, List<TwitchBadgeInfo> badges)
        {
            m_Badges = badges;
            m_ID = id;
            m_Name = name;
            m_DisplayName = displayName;
            m_UserType = userType;
        }

        internal TwitchUser(Type userType) : this(string.Empty, string.Empty, string.Empty, userType, new()) {}
    }
}
