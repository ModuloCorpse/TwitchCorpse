using CorpseLib;
using CorpseLib.DataNotation;
using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using TwitchCorpse.API;
using static TwitchCorpse.TwitchEventSub;
using static TwitchCorpse.TwitchImage;

namespace TwitchCorpse
{
    public class TwitchAPI
    {
        public static readonly Logger TWITCH_API = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-TwitchAPI.log") };
        public static void StartLogging() => TWITCH_API.Start();
        public static void StopLogging() => TWITCH_API.Stop();

        private readonly Dictionary<string, TwitchBadgeSet> m_BadgeSets = [];
        private readonly Dictionary<string, TwitchEmoteSet> m_EmoteSet = [];
        private RefreshToken? m_AccessToken = null;
        private TwitchUser m_SelfUserInfo = new(TwitchUser.Type.BROADCASTER);
        private ITwitchHandler? m_Handler;
        private readonly string[] m_Scopes = [
            "bits:read",
            "channel:bot",
            "channel:manage:broadcast",
            "channel:manage:moderators",
            "channel:manage:polls",
            "channel:manage:redemptions",
            "channel:moderate",
            "channel:read:polls",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "chat:read",
            "chat:edit",
            "moderator:manage:automod",
            "moderator:manage:banned_users",
            "moderator:manage:blocked_terms",
            "moderator:manage:chat_messages",
            "moderator:manage:shoutouts",
            "moderation:read",
            "moderator:read:automod_settings",
            "moderator:read:followers",
            "user:bot",
            "user:read:chat",
            "user:read:email",
            "user:write:chat",
            "whispers:read"
        ];
        private bool m_IsAuthenticated = false;

        public bool IsAuthenticated => m_IsAuthenticated;

        public TwitchAPI() => m_Handler = null;
        public TwitchAPI(ITwitchHandler handler) => m_Handler = handler;

        public void SetHandler(ITwitchHandler handler) => m_Handler = handler;

        public void Authenticate(string publicKey, string privateKey, int port) => AuthenticateWithBrowser(publicKey, privateKey, port, string.Empty, string.Empty);

        public void Authenticate(string publicKey, string privateKey, int port, string pageContent) => AuthenticateWithBrowser(publicKey, privateKey, port, pageContent, string.Empty);

        public void AuthenticateWithBrowser(string publicKey, string privateKey, int port, string browser) => AuthenticateWithBrowser(publicKey, privateKey, port, string.Empty, browser);

        public void AuthenticateWithBrowser(string publicKey, string privateKey, int port, string pageContent, string browser)
        {
            Authenticator authenticator = new("id.twitch.tv", string.Empty, port);
            authenticator.SetPageContent(pageContent);
            OperationResult<RefreshToken> result = authenticator.AuthorizationCode(m_Scopes, publicKey, privateKey, browser);
            if (result)
            {
                m_AccessToken = result.Result!;
                Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", m_AccessToken);
                if (response.StatusCode == 200)
                {
                    TwitchUser? user = GetUserInfo(response.Body, TwitchUser.Type.BROADCASTER);
                    if (user != null)
                    {
                        m_SelfUserInfo = user;
                        m_IsAuthenticated = true;
                    }
                }
            }
        }

        public TwitchEventSub? EventSubConnection(string channelID)
        {
            if (m_AccessToken == null)
                return null;
            return new(this, channelID, m_AccessToken, m_Handler);
        }

        public TwitchEventSub? EventSubConnection(string channelID, SubscriptionType[] subscriptionTypes)
        {
            if (m_AccessToken == null)
                return null;
            return new(this, channelID, m_AccessToken, subscriptionTypes, m_Handler);
        }

        private static Response SendComposedRequest(URLRequest request, RefreshToken? token)
        {
            if (token != null)
                request.AddRefreshToken(token);
            TWITCH_API.Log(string.Format("Sending: {0}", request.Request.ToString()));
            Response response = request.Send();
            if (token != null && response.StatusCode == 401)
            {
                token.Refresh();
                request.AddRefreshToken(token);
                response = request.Send();
            }
            TWITCH_API.Log(string.Format("Received: {0}", response.ToString()));
            return response;
        }

        private static Response SendRequest(Request.MethodType method, string url, DataObject content, RefreshToken? token)
        {
            URLRequest request = new(URI.Parse(url), method, JsonParser.NetStr(content));
            request.AddContentType(MIME.APPLICATION.JSON);
            return SendComposedRequest(request, token);
        }

        private static Response SendRequest(Request.MethodType method, string url, RefreshToken? token) => SendComposedRequest(new(URI.Parse(url), method), token);

        private void LoadBadgeContent(string content)
        {
            DataObject responseJson = JsonParser.Parse(content);
            List<DataObject> datas = responseJson.GetList<DataObject>("data");
            foreach (DataObject data in datas)
            {
                if (data.TryGet("set_id", out string? setID))
                {
                    List<DataObject> versions = data.GetList<DataObject>("versions");
                    foreach (DataObject version in versions)
                    {
                        if (version.TryGet("id", out string? id) &&
                            version.TryGet("image_url_1x", out string? url1x) &&
                            version.TryGet("image_url_2x", out string? url2x) &&
                            version.TryGet("image_url_4x", out string? url4x) &&
                            version.TryGet("title", out string? title) &&
                            version.TryGet("description", out string? description) &&
                            version.TryGet("click_action", out string? clickAction) &&
                            version.TryGet("click_url", out string? clickURL))
                        {
                            TwitchBadgeInfo badgeInfo = new(id!, url1x!, url2x!, url4x!, title!, description!, clickAction!, clickURL ?? string.Empty);
                            if (!m_BadgeSets.ContainsKey(setID!))
                                m_BadgeSets[setID!] = [];
                            m_BadgeSets[setID!].Add(badgeInfo);
                        }
                    }
                }
            }
        }

        private void LoadEmoteSetContent(string emoteSetID, string content)
        {
            DataObject responseJson = JsonParser.Parse(content);
            if (!m_EmoteSet.ContainsKey(emoteSetID))
                m_EmoteSet[emoteSetID] = [];
            if (responseJson.TryGet("template", out string? template))
            {
                List<DataObject> datas = responseJson.GetList<DataObject>("data");
                foreach (DataObject data in datas)
                {
                    List<string> formats = data.GetList<string>("format");
                    List<string> scales = data.GetList<string>("scale");
                    List<string> themeModes = data.GetList<string>("theme_mode");
                    if (data.TryGet("id", out string? id) &&
                        data.TryGet("name", out string? name) &&
                        data.TryGet("emote_type", out string? emoteType) &&
                        formats.Count != 0 && scales.Count != 0 && themeModes.Count != 0)
                    {
                        TwitchImage image = new(name!);
                        foreach (string themeStr in themeModes)
                        {
                            Theme theme = (themeStr == "dark") ? image[Theme.Type.DARK] : image[Theme.Type.LIGHT];
                            foreach (string formatStr in formats)
                            {
                                Format format = (formatStr == "animated") ? theme[Format.Type.ANIMATED] : theme[Format.Type.STATIC];
                                foreach (string scaleStr in scales)
                                {
                                    float scale = 0f;
                                    if (scaleStr == "1.0")
                                        scale = 1f;
                                    else if (scaleStr == "2.0")
                                        scale = 2f;
                                    else if (scaleStr == "3.0")
                                        scale = 4f;
                                    format[scale] = template!.Replace("{{id}}", id).Replace("{{format}}", formatStr).Replace("{{scale}}", scaleStr).Replace("{{theme_mode}}", themeStr);
                                }
                            }
                        }
                        TwitchEmoteInfo info = new(image, id!, name!, emoteType!);
                        m_EmoteSet[emoteSetID].Add(info);
                    }
                }
            }
        }

        public void ResetCache()
        {
            m_BadgeSets.Clear();
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/badges/global", m_AccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
            response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/badges?broadcaster_id={0}", m_SelfUserInfo.ID), m_AccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
            m_EmoteSet.Clear();
            response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/emotes/global", m_AccessToken);
            if (response.StatusCode == 200)
                LoadEmoteSetContent("", response.Body);
        }

        public TwitchBadgeInfo? GetBadge(string badgeSetID, string id)
        {
            if (m_BadgeSets.TryGetValue(badgeSetID, out TwitchBadgeSet? badgeSet) &&
                badgeSet.TryGetBadge(id, out TwitchBadgeInfo? badgeInfo))
                return badgeInfo;
            return null;
        }

        public TwitchEmoteInfo? GetEmote(string emoteSetID, string id)
        {
            if (m_EmoteSet.TryGetValue(emoteSetID, out TwitchEmoteSet? emoteSet))
            {
                if (emoteSet.TryGetEmote(id, out TwitchEmoteInfo? emoteInfo))
                    return emoteInfo;
                return null;
            }
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/emotes/set?emote_set_id={0}", emoteSetID), m_AccessToken);
            if (response.StatusCode == 200)
                LoadEmoteSetContent(emoteSetID, response.Body);
            if (m_EmoteSet.TryGetValue(emoteSetID, out TwitchEmoteSet? loadedEmoteSet) &&
                loadedEmoteSet.TryGetEmote(id, out TwitchEmoteInfo? loadedEmoteInfo))
                return loadedEmoteInfo;
            return null;
        }

        public TwitchUser.Type GetUserType(bool self, bool mod, string type, string id)
        {
            TwitchUser.Type userType = TwitchUser.Type.NONE;
            if (self)
                userType = TwitchUser.Type.SELF;
            else
            {
                if (mod)
                    userType = TwitchUser.Type.MOD;
                switch (type)
                {
                    case "admin": userType = TwitchUser.Type.ADMIN; break;
                    case "global_mod": userType = TwitchUser.Type.GLOBAL_MOD; break;
                    case "staff": userType = TwitchUser.Type.STAFF; break;
                }
                if (id == (m_SelfUserInfo?.ID ?? string.Empty))
                    userType = TwitchUser.Type.BROADCASTER;
            }
            return userType;
        }

        public TwitchUser.Type GetUserType(bool self, string type, string id)
        {
            bool isMod = false;
            if (m_SelfUserInfo != null)
            {
                Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/moderation/moderators?broadcaster_id={0}&user_id={1}", m_SelfUserInfo.ID, id), m_AccessToken);
                if (response.StatusCode == 200)
                {
                    DataObject json = JsonParser.Parse(response.Body);
                    isMod = json.GetList<DataObject>("data").Count != 0;
                }
            }
            return GetUserType(self, isMod, type, id);
        }

        private TwitchUser? GetUserInfo(string content, TwitchUser.Type? userType)
        {
            DataObject responseJson = JsonParser.Parse(content);
            List<DataObject> datas = responseJson.GetList<DataObject>("data");
            if (datas.Count > 0)
            {
                DataObject data = datas[0];
                if (data.TryGet("id", out string? id) &&
                    data.TryGet("login", out string? login) &&
                    data.TryGet("display_name", out string? displayName) &&
                    data.TryGet("type", out string? type) &&
                    data.TryGet("profile_image_url", out string? profileImageURL))
                    return new(id!, login!, displayName!, profileImageURL!, (userType != null) ? (TwitchUser.Type)userType! : GetUserType(false, type!, id!), []);
            }
            return null;
        }

        public TwitchUser GetSelfUserInfo() => m_SelfUserInfo;

        public TwitchUser? GetUserInfoFromLogin(string login)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users?login={0}", login), m_AccessToken);
            if (response.StatusCode == 200)
                return GetUserInfo(response.Body, null);
            return null;
        }

        public TwitchUser? GetUserInfoFromID(string id)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users?id={0}", id), m_AccessToken);
            if (response.StatusCode == 200)
                return GetUserInfo(response.Body, null);
            return null;
        }

        public TwitchChannelInfo? GetChannelInfo(TwitchUser user)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/channels?broadcaster_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                List<DataObject> datas = responseJson.GetList<DataObject>("data");
                if (datas.Count > 0)
                {
                    DataObject data = datas[0];
                    if (data.TryGet("game_id", out string? gameID) &&
                        data.TryGet("game_name", out string? gameName) &&
                        data.TryGet("title", out string? title) &&
                        data.TryGet("broadcaster_language", out string? language))
                        return new TwitchChannelInfo(user, gameID!, gameName!, title!, language!);
                }
            }
            return null;
        }

        public bool SetChannelInfo(TwitchUser user, string title, string gameID, string language = "")
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(gameID) && string.IsNullOrEmpty(language))
                return false;
            DataObject body = [];
            if (!string.IsNullOrEmpty(title))
                body.Add("title", title);
            if (!string.IsNullOrEmpty(gameID))
                body.Add("game_id", gameID);
            if (!string.IsNullOrEmpty(language))
                body.Add("broadcaster_language", language);
            Response response = SendRequest(Request.MethodType.PATCH, string.Format("https://api.twitch.tv/helix/channels?broadcaster_id={0}", user.ID), body, m_AccessToken);
            return response.StatusCode == 204;
        }

        public List<TwitchUser> GetChannelFollowedByID(TwitchUser user)
        {
            List<TwitchUser> ret = [];
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users/follows?from_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("to_id", out string? toID) &&
                        data.TryGet("to_login", out string? toLogin) &&
                        data.TryGet("to_name", out string? toName))
                        ret.Add(new(toID!, toLogin!, toName!, string.Empty, GetUserType(false, string.Empty, toID!), []));
                }
            }
            return ret;
        }

        public List<TwitchCategoryInfo> SearchCategoryInfo(string query)
        {
            List<TwitchCategoryInfo> ret = [];
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/search/categories?query={0}", URI.Encode(query)), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("id", out string? id) &&
                        data.TryGet("name", out string? name) &&
                        data.TryGet("box_art_url", out string? imageURL))
                        ret.Add(new(id!, name!, imageURL!));
                }
            }
            return ret;
        }

        public TwitchCategoryInfo? GetCategoryInfo(string categoryID, string categoryName)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/search/categories?query={0}", URI.Encode(categoryName)), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("id", out string? id) && id == categoryID &&
                        data.TryGet("name", out string? name) &&
                        data.TryGet("box_art_url", out string? imageURL))
                        return new(id!, name!, imageURL!);
                }
            }
            return null;
        }

        public bool ManageHeldMessage(string messageID, bool allow)
        {
            if (m_SelfUserInfo == null)
                return false;
            DataObject json = new()
            {
                { "user_id", m_SelfUserInfo.ID },
                { "msg_id", messageID },
                { "action", (allow) ? "ALLOW" : "DENY" }
            };
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/moderation/automod/message", json, m_AccessToken);
            return response.StatusCode == 204;
        }

        public bool BanUser(TwitchUser user, string reason, uint duration = 0)
        {
            if (m_SelfUserInfo == null)
                return false;
            DataObject data = new()
            {
                { "user_id", user.ID },
                { "reason", reason }
            };
            if (duration > 0)
                data.Add("duration", duration);
            DataObject json = new()
            {
                { "data", data }
            };
            Response response = SendRequest(Request.MethodType.POST, string.Format("https://api.twitch.tv/helix/moderation/bans?broadcaster_id={0}&moderator_id={0}", m_SelfUserInfo.ID), json, m_AccessToken);
            return response.StatusCode == 204;
        }

        public bool UnbanUser(string moderatorID, string userID)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response response = SendRequest(Request.MethodType.DELETE, string.Format("https://api.twitch.tv/helix/moderation/bans?broadcaster_id={0}&moderator_id={1}&user_id={2}", m_SelfUserInfo.ID, moderatorID, userID), m_AccessToken);
            return response.StatusCode == 204;
        }

        public TwitchStreamInfo? GetStreamInfoByID(TwitchUser user)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/streams?user_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("id", out string? id) && user.ID == id &&
                        data.TryGet("user_id", out string? userID) &&
                        data.TryGet("user_login", out string? userLogin) &&
                        data.TryGet("user_name", out string? userName) &&
                        data.TryGet("game_id", out string? gameID) &&
                        data.TryGet("game_name", out string? gameName) &&
                        data.TryGet("title", out string? title) &&
                        data.TryGet("viewer_count", out int? viewerCount) &&
                        data.TryGet("language", out string? language) &&
                        data.TryGet("thumbnail_url", out string? thumbnailURL) &&
                        data.TryGet("is_mature", out bool? isMature))
                        return new(new(userID!, userLogin!, userName!, string.Empty, GetUserType(false, string.Empty, userID!), []), data.GetList<string>("tags"), id!, gameID!, gameName!, title!, language!, thumbnailURL!, (int)viewerCount!, (bool)isMature!);
                }
            }
            return null;
        }

        public bool StartCommercial(uint duration)
        {
            if (m_SelfUserInfo == null)
                return false;
            DataObject json = new()
            {
                { "broadcaster_id", m_SelfUserInfo.ID },
                { "length", duration }
            };
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/channels/commercial", json, m_AccessToken);
            return response.StatusCode == 200;
        }

        public static void LoadCheermoteFormat(DataObject obj, Theme theme, Format.Type formatType)
        {
            Format format = theme[formatType];
            if (obj.TryGet("1", out string? url1x))
                format[1] = url1x!;
            if (obj.TryGet("1.5", out string? url1x5))
                format[1.5f] = url1x5!;
            if (obj.TryGet("2", out string? url2x))
                format[2] = url2x!;
            if (obj.TryGet("3", out string? url3x))
                format[3] = url3x!;
            if (obj.TryGet("4", out string? url4x))
                format[4] = url4x!;
        }

        public static void LoadCheermoteTheme(DataObject obj, TwitchImage image, Theme.Type themeType)
        {
            Theme theme = image[themeType];
            if (obj.TryGet("animated", out DataObject? animated))
                LoadCheermoteFormat(animated!, theme, Format.Type.ANIMATED);
            if (obj.TryGet("static", out DataObject? @static))
                LoadCheermoteFormat(@static!, theme, Format.Type.STATIC);
        }

        public TwitchCheermote[] GetTwitchCheermotes()
        {
            List<TwitchCheermote> ret = [];
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/bits/cheermotes?broadcaster_id={0}", m_SelfUserInfo.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("prefix", out string? prefix))
                    {
                        TwitchCheermote twitchCheermote = new(prefix!);
                        foreach (DataObject tier in data.GetList<DataObject>("tiers"))
                        {
                            if (tier.TryGet("min_bits", out int? threshold) &&
                                tier.TryGet("can_cheer", out bool? canCheer) &&
                                tier.TryGet("images", out DataObject? images))
                            {
                                TwitchImage image = new(string.Format("{0}{1}", prefix!, threshold));
                                if (images!.TryGet("dark", out DataObject? dark))
                                    LoadCheermoteTheme(dark!, image, TwitchImage.Theme.Type.DARK);
                                if (images!.TryGet("light", out DataObject? light))
                                    LoadCheermoteTheme(light!, image, TwitchImage.Theme.Type.LIGHT);
                                twitchCheermote.AddTier(new(image, (int)threshold!, (bool)canCheer!));
                            }
                        }
                        ret.Add(twitchCheermote);
                    }
                }
            }
            return [.. ret];
        }

        private string PostChatMessage(DataObject chatMessage)
        {
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/chat/messages", chatMessage, m_AccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                foreach (DataObject data in responseJson.GetList<DataObject>("data"))
                {
                    if (data.TryGet("is_sent", out bool? isSent) && (bool)isSent! &&
                        data.TryGet("message_id", out string? id))
                        return id!;
                }
            }
            return string.Empty;
        }

        public string PostMessage(TwitchUser broadcaster, string message)
        {
            return PostChatMessage(new()
            {
                { "broadcaster_id", broadcaster.ID },
                { "sender_id", m_SelfUserInfo.ID },
                { "message", message }
            });
        }

        public string ReplyMessage(TwitchUser broadcaster, string message, string replyMessageID)
        {
            return PostChatMessage(new()
            {
                { "broadcaster_id", broadcaster.ID },
                { "sender_id", m_SelfUserInfo.ID },
                { "message", message },
                { "reply_parent_message_id", replyMessageID }
            });
        }
    }
}
