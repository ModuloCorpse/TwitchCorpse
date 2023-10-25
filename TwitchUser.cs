using CorpseLib.Json;
using CorpseLib;

namespace TwitchCorpse
{
    public class TwitchUser
    {
        public class JSerializer : AJSerializer<TwitchUser>
        {
            protected override OperationResult<TwitchUser> Deserialize(JObject reader)
            {
                if (reader.TryGet("id", out string? id))
                {
                    if (reader.TryGet("name", out string? name))
                    {
                        if (reader.TryGet("display_name", out string? displayName))
                        {
                            if (reader.TryGet("user_type", out TwitchUser.Type? userType))
                            {
                                return new(new(id!, name!, displayName!, (TwitchUser.Type)userType!));
                            }
                            return new("Bad json", "Missing twitch user type");
                        }
                        return new("Bad json", "Missing twitch user display name");
                    }
                    return new("Bad json", "Missing twitch user name");
                }
                return new("Bad json", "Missing twitch user id");
            }

            protected override void Serialize(TwitchUser obj, JObject writer)
            {
                writer["id"] = obj.ID;
                writer["name"] = obj.Name;
                writer["display_name"] = obj.DisplayName;
                writer["user_type"] = obj.UserType;
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

        private readonly string m_ID;
        private readonly string m_Name;
        private readonly string m_DisplayName;
        private readonly Type m_UserType;

        public string ID => m_ID;
        public string Name => m_Name;
        public string DisplayName => m_DisplayName;
        public Type UserType => m_UserType;

        public TwitchUser(string id, string name, string displayName, Type userType)
        {
            m_ID = id;
            m_Name = name;
            m_DisplayName = displayName;
            m_UserType = userType;
        }

        public TwitchUser(string id, string name, Type userType) : this(id, name, name, userType) {}

        public TwitchUser(string id, string name, string displayName) : this(id, name, displayName, Type.NONE) { }
    }
}
