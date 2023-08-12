using CorpseLib;
using CorpseLib.Json;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using System.Web;

namespace TwitchCorpse
{
    public class API
    {
        private static readonly Authenticator ms_TwitchAuthenticator = new("id.twitch.tv", "/twitch_authenticate");

        public static OperationResult<RefreshToken> Authenticate(string[] expectedScope, string publicKey, string privateKey, string browser = "")
        {
            return ms_TwitchAuthenticator.AuthorizationCode(expectedScope, publicKey, privateKey, browser);
        }

        private readonly Dictionary<string, EmoteInfo> m_CachedEmotes = new();
        private readonly Dictionary<string, User> m_CachedUserInfoFromLogin = new();
        private readonly Dictionary<string, User> m_CachedUserInfoFromID = new();
        private readonly HashSet<string> m_LoadedChannelIDEmoteSets = new();
        private readonly HashSet<string> m_LoadedEmoteSets = new();
        private RefreshToken? m_AccessToken = null;
        private User m_SelfUserInfo = new(string.Empty, string.Empty, User.Type.BROADCASTER);
        private string m_EmoteURLTemplate = string.Empty;

        private static Response? SendRequest(Request.MethodType method, string url, JFile content, RefreshToken? token)
        {
            URLRequest request = new(URI.Parse(url), method, content.ToNetworkString());
            request.AddContentType(MIME.APPLICATION.JSON);
            if (token != null)
            {
                request.AddHeaderField("Authorization", string.Format("Bearer {0}", token.AccessToken));
                request.AddHeaderField("Client-Id", token.ClientID);
            }
            return request.Send();
        }

        private static Response? SendRequest(Request.MethodType method, string url, RefreshToken? token)
        {
            URLRequest request = new(URI.Parse(url), method);
            if (token != null)
            {
                request.AddHeaderField("Authorization", string.Format("Bearer {0}", token.AccessToken));
                request.AddHeaderField("Client-Id", token.ClientID);
            }
            return request.Send();
        }

        internal void Authenticate(RefreshToken accessToken)
        {
            m_AccessToken = accessToken;
            Response? response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", accessToken);
            if (response != null)
            {
                User? user = GetUserInfo(response.Body, User.Type.BROADCASTER);
                if (user != null)
                {
                    m_SelfUserInfo = user;
                    LoadChannelEmoteSet(user);
                }
            }
        }

        private JFile LoadEmoteSetContent(string content)
        {
            JFile responseJson = new(content);
            List<JObject> datas = responseJson.GetList<JObject>("data");
            foreach (JObject data in datas)
            {
                List<string> format = data.GetList<string>("format");
                List<string> scale = data.GetList<string>("scale");
                List<string> themeMode = data.GetList<string>("theme_mode");
                if (data.TryGet("id", out string? id) &&
                    data.TryGet("name", out string? name) &&
                    data.TryGet("emote_type", out string? emoteType) &&
                    format.Count != 0 && scale.Count != 0 && themeMode.Count != 0)
                {
                    EmoteInfo info = new(id!, name!, emoteType!, format, scale, themeMode);
                    m_CachedEmotes[info.ID] = info;
                }
            }
            return responseJson;
        }

        public void LoadGlobalEmoteSet()
        {
            Response? response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/emotes/global", m_AccessToken);
            if (response != null)
            {
                JFile responseJson = LoadEmoteSetContent(response.Body);
                if (responseJson.TryGet("template", out string? template))
                    m_EmoteURLTemplate = template!;
            }
        }

        public void LoadChannelEmoteSet(User user)
        {
            if (m_LoadedChannelIDEmoteSets.Contains(user.ID))
                return;
            m_LoadedChannelIDEmoteSets.Add(user.ID);
            Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/emotes?broadcaster_id={0}", user.ID), m_AccessToken);
            if (response != null)
                LoadEmoteSetContent(response.Body);
        }

        public void LoadEmoteSet(string emoteSetID)
        {
            if (m_LoadedEmoteSets.Contains(emoteSetID))
                return;
            m_LoadedEmoteSets.Add(emoteSetID);
            Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/emotes/set?emote_set_id={0}", emoteSetID), m_AccessToken);
            if (response != null)
                LoadEmoteSetContent(response.Body);
        }

        public void LoadEmoteSetFromFollowedChannel(User user)
        {
            List<User> followedBy = GetChannelFollowedByID(user);
            foreach (User followed in followedBy)
                LoadChannelEmoteSet(followed);
        }

        public string GetEmoteURL(string id, bool isAmimated, byte scale, bool isDarkMode)
        {
            EmoteInfo? emoteInfo = (m_CachedEmotes.TryGetValue(id, out EmoteInfo? info)) ? info : null; ;
            if (emoteInfo != null)
            {
                string format = (isAmimated) ? "animated" : "static";
                string emoteScale = scale switch
                {
                    3 => "3.0",
                    2 => "2.0",
                    _ => "1.0"
                };
                string theme_mode = (isDarkMode) ? "dark" : "light";
                return m_EmoteURLTemplate.Replace("{{id}}", id).Replace("{{format}}", format).Replace("{{scale}}", emoteScale).Replace("{{theme_mode}}", theme_mode);
            }
            return string.Empty;
        }

        public EmoteInfo? GetEmoteFromID(string id)
        {
            EmoteInfo? emoteInfo = (m_CachedEmotes.TryGetValue(id, out EmoteInfo? info)) ? info : null; ;
            if (emoteInfo != null)
                return emoteInfo;
            return null;
        }

        public User.Type GetUserType(bool self, bool mod, string type, string id)
        {
            User.Type userType = User.Type.NONE;
            if (self)
                userType = User.Type.SELF;
            else
            {
                if (mod)
                    userType = User.Type.MOD;
                switch (type)
                {
                    case "admin": userType = User.Type.ADMIN; break;
                    case "global_mod": userType = User.Type.GLOBAL_MOD; break;
                    case "staff": userType = User.Type.STAFF; break;
                }
                if (id == (m_SelfUserInfo?.ID ?? string.Empty))
                    userType = User.Type.BROADCASTER;
            }
            return userType;
        }

        public User.Type GetUserType(bool self, string type, string id)
        {
            bool isMod = false;
            if (m_SelfUserInfo != null)
            {
                Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/moderation/moderators?broadcaster_id={0}&user_id={1}", m_SelfUserInfo.ID, id), m_AccessToken);
                if (response != null)
                {
                    JFile json = new(response.Body);
                    isMod = json.GetList<JObject>("data").Count != 0;
                }
            }
            return GetUserType(self, isMod, type, id);
        }

        private User? GetUserInfo(string content, User.Type? userType)
        {
            JFile responseJson = new(content);
            List<JObject> datas = responseJson.GetList<JObject>("data");
            if (datas.Count > 0)
            {
                JObject data = datas[0];
                if (data.TryGet("id", out string? id) &&
                    data.TryGet("login", out string? login) &&
                    data.TryGet("display_name", out string? displayName) &&
                    data.TryGet("type", out string? type))
                    return new(id!, login!, displayName!, (userType != null) ? (User.Type)userType! : GetUserType(false, type!, id!));
            }
            return null;
        }

        public User? GetUserInfoOfToken(RefreshToken token)
        {
            Response? response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", token);
            if (response != null)
                return GetUserInfo(response.Body, User.Type.SELF);
            return null;
        }

        public User GetSelfUserInfo() => m_SelfUserInfo;

        public User? GetUserInfoFromLogin(string login)
        {
            User? userInfo = (m_CachedUserInfoFromLogin.TryGetValue(login, out User? info)) ? info : null; ;
            if (userInfo != null)
                return userInfo;
            Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users?login={0}", login), m_AccessToken);
            if (response != null)
            {
                User? ret = GetUserInfo(response.Body, null);
                if (ret != null)
                {
                    m_CachedUserInfoFromID[ret.ID] = ret;
                    m_CachedUserInfoFromLogin[ret.Name] = ret;
                }
                return ret;
            }
            return null;
        }

        public ChannelInfo? GetChannelInfo(string login)
        {
            ChannelInfo? channelInfo = null;
            if (channelInfo != null)
                return channelInfo;
            User? broadcasterInfo = GetUserInfoFromLogin(login);
            if (broadcasterInfo != null)
            {
                Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/channels?broadcaster_id={0}", broadcasterInfo.ID), m_AccessToken);
                if (response != null)
                {
                    JFile responseJson = new(response.Body);
                    List<JObject> datas = responseJson.GetList<JObject>("data");
                    if (datas.Count > 0)
                    {
                        JObject data = datas[0];
                        if (data.TryGet("game_id", out string? gameID) &&
                            data.TryGet("game_name", out string? gameName) &&
                            data.TryGet("title", out string? title) &&
                            data.TryGet("broadcaster_language", out string? language))
                            return new ChannelInfo(broadcasterInfo, gameID!, gameName!, title!, language!);
                    }
                }
            }
            return null;
        }

        public bool SetChannelInfo(User user, string title, string gameID, string language = "")
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(gameID) && string.IsNullOrEmpty(language))
                return false;
            JFile body = new();
            if (!string.IsNullOrEmpty(title))
                body.Set("title", title);
            if (!string.IsNullOrEmpty(gameID))
                body.Set("game_id", gameID);
            if (!string.IsNullOrEmpty(language))
                body.Set("broadcaster_language", language);
            Response? response = SendRequest(Request.MethodType.PATCH, string.Format("https://api.twitch.tv/helix/channels?broadcaster_id={0}", user.ID), body, m_AccessToken);
            if (response != null)
                return response.StatusCode == 204;
            return false;
        }

        public List<User> GetChannelFollowedByID(User user)
        {
            List<User> ret = new();
            Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users/follows?from_id={0}", user.ID), m_AccessToken);
            if (response != null)
            {
                JFile responseJson = new(response.Body);
                foreach (JObject data in responseJson.GetList<JObject>("data"))
                {
                    if (data.TryGet("to_id", out string? toID) &&
                        data.TryGet("to_login", out string? toLogin) &&
                        data.TryGet("to_name", out string? toName))
                        ret.Add(new(toID!, toLogin!, toName!, GetUserType(false, string.Empty, toID!)));
                }
            }
            return ret;
        }

        public List<CategoryInfo> SearchCategoryInfo(string query)
        {
            List<CategoryInfo> ret = new();
            Response? response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/search/categories?query={0}", HttpUtility.UrlEncode(query)), m_AccessToken);
            if (response != null)
            {
                JFile responseJson = new(response.Body);
                foreach (JObject data in responseJson.GetList<JObject>("data"))
                {
                    if (data.TryGet("id", out string? id) &&
                        data.TryGet("name", out string? name) &&
                        data.TryGet("box_art_url", out string? imageURL))
                        ret.Add(new(id!, name!, imageURL!));
                }
            }
            return ret;
        }

        public bool ManageHeldMessage(string messageID, bool allow)
        {
            if (m_SelfUserInfo == null)
                return false;
            JFile json = new();
            json.Set("user_id", m_SelfUserInfo.ID);
            json.Set("msg_id", messageID);
            json.Set("action", (allow) ? "ALLOW" : "DENY");
            Response? response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/moderation/automod/message", json, m_AccessToken);
            if (response != null)
                return response.StatusCode == 204;
            return false;
        }

        public bool BanUser(User user, string reason, uint duration = 0)
        {
            if (m_SelfUserInfo == null)
                return false;
            JFile json = new();
            JFile data = new();
            data.Set("user_id", user.ID);
            if (duration > 0)
                data.Set("duration", duration);
            data.Set("reason", reason);
            json.Set("data", data);
            Response? response = SendRequest(Request.MethodType.POST, string.Format("https://api.twitch.tv/helix/moderation/bans?broadcaster_id={0}&moderator_id={0}", m_SelfUserInfo.ID), json, m_AccessToken);
            if (response != null)
                return response.StatusCode == 204;
            return false;
        }

        public bool UnbanUser(string moderatorID, string userID)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response? response = SendRequest(Request.MethodType.DELETE, string.Format("https://api.twitch.tv/helix/moderation/bans?broadcaster_id={0}&moderator_id={1}&user_id={2}", m_SelfUserInfo.ID, moderatorID, userID), m_AccessToken);
            if (response != null)
                return response.StatusCode == 204;
            return false;
        }
    }
}
