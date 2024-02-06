using CorpseLib.Json;
using CorpseLib;
using TwitchCorpse.API;

namespace TwitchCorpse
{
    public class TwitchUser(string id, string name, string displayName, string profileImageURL, TwitchUser.Type userType, List<TwitchBadgeInfo> badges)
    {
        public class JSerializer : AJSerializer<TwitchUser>
        {
            protected override OperationResult<TwitchUser> Deserialize(JObject reader)
            {
                if (reader.TryGet("id", out string? id) &&
                    reader.TryGet("name", out string? name) &&
                    reader.TryGet("display_name", out string? displayName) &&
                    reader.TryGet("profile_image_url", out string? profileImageURL) &&
                    reader.TryGet("user_type", out Type? userType))
                    return new(new(id!, name!, displayName!, profileImageURL!, (Type)userType!, reader.GetList<TwitchBadgeInfo>("badges")));
                return new("Bad json", string.Empty);
            }

            protected override void Serialize(TwitchUser obj, JObject writer)
            {
                writer["id"] = obj.m_ID;
                writer["name"] = obj.m_Name;
                writer["display_name"] = obj.m_DisplayName;
                writer["profile_image_url"] = obj.m_DisplayName;
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

        private readonly List<TwitchBadgeInfo> m_Badges = badges;
        private readonly string m_ID = id;
        private readonly string m_Name = name;
        private readonly string m_DisplayName = displayName;
        private readonly string m_ProfileImageURL = profileImageURL;
        private readonly Type m_UserType = userType;

        public TwitchBadgeInfo[] Badges => m_Badges.ToArray();
        public string ID => m_ID;
        public string Name => m_Name;
        public string DisplayName => m_DisplayName;
        public string ProfileImageURL => m_ProfileImageURL;
        public Type UserType => m_UserType;

        internal TwitchUser(Type userType) : this(string.Empty, string.Empty, string.Empty, string.Empty, userType, []) {}
    }
}
