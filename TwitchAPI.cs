using CorpseLib;
using CorpseLib.DataNotation;
using CorpseLib.Encryption;
using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using TwitchCorpse.API;
using static TwitchCorpse.TwitchEventSub;
using static TwitchCorpse.API.TwitchEmoteImage;

namespace TwitchCorpse
{
    public class TwitchAPI
    {
        public static readonly Logger TWITCH_API = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-TwitchAPI.log") };
        public static void StartLogging() => TWITCH_API.Start();
        public static void StopLogging() => TWITCH_API.Stop();

        private readonly Dictionary<string, TwitchBadgeSet> m_BadgeSets = [];
        private readonly Dictionary<string, TwitchEmoteSet> m_EmoteSet = [];
        private readonly Authenticator m_Authenticator;
        private RefreshToken? m_UserAccessToken = null;
        private RefreshToken? m_AppAccessToken = null;
        private TwitchUser m_SelfUserInfo = new(TwitchUser.Type.BROADCASTER);
        private ITwitchHandler? m_Handler;
        private readonly string[] m_Scopes = [
            "bits:read",
            "channel:bot",
            "channel:edit:commercial",
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

        public TwitchAPI(string publicKey, string privateKey, int port, string pageContent)
        {
            m_Authenticator = new(m_Scopes, publicKey, privateKey, "id.twitch.tv", string.Empty, port);
            m_Authenticator.SetPageContent(pageContent);
            m_Handler = null;
        }

        public TwitchAPI(string publicKey, string privateKey, int port)
        {
            m_Authenticator = new(m_Scopes, publicKey, privateKey, "id.twitch.tv", string.Empty, port);
            m_Handler = null;
        }

        public TwitchAPI(string publicKey, string privateKey, int port, string pageContent, ITwitchHandler handler)
        {
            m_Authenticator = new(m_Scopes, publicKey, privateKey, "id.twitch.tv", string.Empty, port);
            m_Authenticator.SetPageContent(pageContent);
            m_Handler = handler;
        }

        public TwitchAPI(string publicKey, string privateKey, int port, ITwitchHandler handler)
        {
            m_Authenticator = new(m_Scopes, publicKey, privateKey, "id.twitch.tv", string.Empty, port);
            m_Handler = handler;
        }

        public void SetHandler(ITwitchHandler handler) => m_Handler = handler;

        public void Authenticate(RefreshToken token)
        {
            m_UserAccessToken = token;
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", m_UserAccessToken);
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

        public void AppAuthenticate(TwitchAPI api) => m_AppAccessToken = api.m_AppAccessToken;

        public void AppAuthenticate()
        {
            OperationResult<RefreshToken> result = m_Authenticator.ClientCredentials();
            if (result)
                m_AppAccessToken = result.Result!;
        }

        public void AuthenticateWithBrowser(string browser = "")
        {
            OperationResult<RefreshToken> result = m_Authenticator.AuthorizationCode(browser);
            if (result)
                Authenticate(result.Result!);
        }

        public void AuthenticateFromVault(LocalVault vault, string key)
        {
            RefreshToken? refreshToken = m_Authenticator.LoadToken(vault, key);
            if (refreshToken != null)
            {
                Authenticate(refreshToken);
                if (m_IsAuthenticated && m_UserAccessToken != null)
                {
                    m_UserAccessToken.Refreshed += (token) =>
                    {
                        if (token is RefreshToken refreshedToken)
                            m_Authenticator.StoreToken(vault, key, refreshedToken);
                    };
                }
            }
        }

        public void AuthenticateFromTokenFile(string path)
        {
            RefreshToken? refreshToken = m_Authenticator.LoadToken(path);
            if (refreshToken != null)
                Authenticate(refreshToken);
        }

        public void SaveAPIToken(LocalVault vault, string key)
        {
            if (m_UserAccessToken != null)
            {
                m_UserAccessToken.Refresh();
                m_Authenticator.StoreToken(vault, key, m_UserAccessToken);
            }
        }

        public void SaveAPIToken(string path)
        {
            if (m_UserAccessToken != null)
            {
                m_UserAccessToken.Refresh();
                m_Authenticator.SaveToken(path, m_UserAccessToken);
            }
        }

        public TwitchEventSub? EventSubConnection(string channelID)
        {
            if (m_UserAccessToken == null)
                return null;
            return new(this, channelID, m_UserAccessToken, m_Handler);
        }

        public TwitchEventSub? EventSubConnection(string channelID, SubscriptionType[] subscriptionTypes)
        {
            if (m_UserAccessToken == null)
                return null;
            return new(this, channelID, m_UserAccessToken, subscriptionTypes, m_Handler);
        }

        private static Response SendComposedRequest(URLRequest request, RefreshToken? token)
        {
            if (token != null)
                request.AddRefreshToken(token);
            TWITCH_API.Log("Sending: ${0}", request.Request);
            Response response = request.Send();
            if (token != null && response.StatusCode == 401)
            {
                token.Refresh();
                request.AddRefreshToken(token);
                response = request.Send();
            }
            TWITCH_API.Log("Received: ${0}", response);
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
                            TwitchImage badgeImage = new();
                            badgeImage[1] = url1x!;
                            badgeImage[2] = url2x!;
                            badgeImage[4] = url4x!;
                            TwitchBadgeInfo badgeInfo = new(id!, badgeImage, title!, description!, clickAction!, clickURL ?? string.Empty);
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
                        TwitchEmoteImage image = new(name!);
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
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/badges/global", m_UserAccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
            response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/chat/badges?broadcaster_id={m_SelfUserInfo.ID}", m_UserAccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
            m_EmoteSet.Clear();
            response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/emotes/global", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/chat/emotes/set?emote_set_id={emoteSetID}", m_UserAccessToken);
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
                Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/moderation/moderators?broadcaster_id={m_SelfUserInfo.ID}&user_id={id}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/users?login={login}", m_UserAccessToken);
            if (response.StatusCode == 200)
                return GetUserInfo(response.Body, null);
            return null;
        }

        public TwitchUser? GetUserInfoFromID(string id)
        {
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/users?id={id}", m_UserAccessToken);
            if (response.StatusCode == 200)
                return GetUserInfo(response.Body, null);
            return null;
        }

        public TwitchChannelInfo? GetChannelInfo() => GetChannelInfo(m_SelfUserInfo);
        public TwitchChannelInfo? GetChannelInfo(TwitchUser user)
        {
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/channels?broadcaster_id={user.ID}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.PATCH, $"https://api.twitch.tv/helix/channels?broadcaster_id={user.ID}", body, m_UserAccessToken);
            return response.StatusCode == 204;
        }

        public List<TwitchUser> GetChannelFollowedByID(TwitchUser user)
        {
            List<TwitchUser> ret = [];
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/users/follows?from_id={user.ID}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/search/categories?query={URI.Encode(query)}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/search/categories?query={URI.Encode(categoryName)}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/moderation/automod/message", json, m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.POST, $"https://api.twitch.tv/helix/moderation/bans?broadcaster_id={m_SelfUserInfo.ID}&moderator_id={m_SelfUserInfo.ID}", json, m_UserAccessToken);
            return response.StatusCode == 204;
        }

        public bool UnbanUser(string moderatorID, string userID)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response response = SendRequest(Request.MethodType.DELETE, $"https://api.twitch.tv/helix/moderation/bans?broadcaster_id={m_SelfUserInfo.ID}&moderator_id={moderatorID}&user_id={userID}", m_UserAccessToken);
            return response.StatusCode == 204;
        }

        public TwitchStreamInfo? GetStreamInfoByID(TwitchUser user)
        {
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/streams?user_id={user.ID}", m_UserAccessToken);
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
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/channels/commercial", json, m_UserAccessToken);
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

        public static void LoadCheermoteTheme(DataObject obj, TwitchEmoteImage image, Theme.Type themeType)
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
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/bits/cheermotes?broadcaster_id={m_SelfUserInfo.ID}", m_UserAccessToken);
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
                                TwitchEmoteImage image = new($"{prefix!}{threshold}");
                                if (images!.TryGet("dark", out DataObject? dark))
                                    LoadCheermoteTheme(dark!, image, Theme.Type.DARK);
                                if (images!.TryGet("light", out DataObject? light))
                                    LoadCheermoteTheme(light!, image, Theme.Type.LIGHT);
                                twitchCheermote.AddTier(new(image, (int)threshold!, (bool)canCheer!));
                            }
                        }
                        ret.Add(twitchCheermote);
                    }
                }
            }
            return [.. ret];
        }

        private string PostChatMessage(DataObject chatMessage, bool forSourceOnly)
        {
            Response response;
            if (m_AppAccessToken != null)
            {
                chatMessage.Add("for_source_only", forSourceOnly);
                response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/chat/messages", chatMessage, m_AppAccessToken);
            }
            else
            {
                response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/chat/messages", chatMessage, m_UserAccessToken);
            }
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

        public string PostMessage(TwitchUser broadcaster, string message, bool forSourceOnly = true)
        {
            //TODO maybe HTML encode the message
            return PostChatMessage(new()
            {
                { "broadcaster_id", broadcaster.ID },
                { "sender_id", m_SelfUserInfo.ID },
                { "message", message }
            }, forSourceOnly);
        }

        public string ReplyMessage(TwitchUser broadcaster, string message, string replyMessageID, bool forSourceOnly = true)
        {
            return PostChatMessage(new()
            {
                { "broadcaster_id", broadcaster.ID },
                { "sender_id", m_SelfUserInfo.ID },
                { "message", message },
                { "reply_parent_message_id", replyMessageID }
            }, forSourceOnly);
        }

        public bool ShoutoutUser(TwitchUser user)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response response = SendRequest(Request.MethodType.POST, $"https://api.twitch.tv/helix/chat/shoutouts?from_broadcaster_id={m_SelfUserInfo.ID}&to_broadcaster_id={user.ID}&moderator_id={m_SelfUserInfo.ID}", m_UserAccessToken);
            return response.StatusCode == 204;
        }

        public bool DeleteMessage(string messageID)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response response = SendRequest(Request.MethodType.DELETE, $"https://api.twitch.tv/helix/moderation/chat?broadcaster_id={m_SelfUserInfo.ID}&moderator_id={m_SelfUserInfo.ID}&message_id={messageID}", m_UserAccessToken);
            return response.StatusCode == 204;
        }

        private static bool LoadTwitchImage(DataObject reader, out TwitchImage? image)
        {
            if (reader.TryGet("url_1x", out string? url1x) &&
                reader.TryGet("url_2x", out string? url2x) &&
                reader.TryGet("url_4x", out string? url4x))
            {
                image = new();
                image[1] = url1x!;
                image[2] = url2x!;
                image[4] = url4x!;
                return true;
            }
            image = null;
            return false;
        }

        private static TwitchRewardInfo? LoadTwitchRemoteInfo(DataObject reader)
        {
            TwitchImage? image = null;
            if (reader.TryGet("image", out DataObject? imageObject))
                LoadTwitchImage(imageObject!, out image);
            DateTime? cooldownExpiresAt = null;
            if (reader.TryGet("cooldown_expires_at", out string? cooldownExpiresAtStr))
            {
                if (DateTime.TryParse(cooldownExpiresAtStr, out DateTime cooldownDateTime))
                    cooldownExpiresAt = cooldownDateTime;
            }

            int globalCooldownSeconds = -1;
            if (reader.TryGet("global_cooldown_setting", out DataObject? globalCooldownSettingObject))
            {
                if (globalCooldownSettingObject!.TryGet("is_enabled", out bool isGlobalCooldownEnabled) && isGlobalCooldownEnabled && globalCooldownSettingObject.TryGet("global_cooldown_seconds", out int globalCooldownSecondsValue))
                    globalCooldownSeconds = globalCooldownSecondsValue;
            }
            else if (reader.TryGet("global_cooldown", out DataObject? globalCooldownObject))
            {
                if (globalCooldownObject!.TryGet("is_enabled", out bool isGlobalCooldownEnabled) && isGlobalCooldownEnabled && globalCooldownObject.TryGet("seconds", out int globalCooldownSecondsValue))
                    globalCooldownSeconds = globalCooldownSecondsValue;
            }

            int maxPerStream = -1;
            if (reader.TryGet("max_per_stream_setting", out DataObject? maxPerStreamSettingObject))
            {
                if (maxPerStreamSettingObject!.TryGet("is_enabled", out bool isMaxPerStreamEnabled) && isMaxPerStreamEnabled && maxPerStreamSettingObject.TryGet("max_per_stream", out int maxPerStreamValue))
                    maxPerStream = maxPerStreamValue;
            }
            else if (reader.TryGet("max_per_stream", out DataObject? maxPerStreamObject))
            {
                if (maxPerStreamObject!.TryGet("is_enabled", out bool isMaxPerStreamEnabled) && isMaxPerStreamEnabled && maxPerStreamObject.TryGet("value", out int maxPerStreamValue))
                    maxPerStream = maxPerStreamValue;
            }

            int maxPerUserPerStream = -1;
            if (reader.TryGet("max_per_user_per_stream_setting", out DataObject? maxPerUserPerStreamSettingObject))
            {
                if (maxPerUserPerStreamSettingObject!.TryGet("is_enabled", out bool isMaxPerUserPerStreamEnabled) && isMaxPerUserPerStreamEnabled && maxPerUserPerStreamSettingObject.TryGet("max_per_user_per_stream", out int maxPerUserPerStreamValue))
                    maxPerUserPerStream = maxPerUserPerStreamValue;
            }
            else if (reader.TryGet("max_per_user_per_stream", out DataObject? maxPerUserPerStreamObject))
            {
                if (maxPerUserPerStreamObject!.TryGet("is_enabled", out bool isMaxPerUserPerStreamEnabled) && isMaxPerUserPerStreamEnabled && maxPerUserPerStreamObject.TryGet("value", out int maxPerUserPerStreamValue))
                    maxPerUserPerStream = maxPerUserPerStreamValue;
            }

            reader.TryGet("redemptions_redeemed_current_stream", out int? redemptionsRedeemedCurrentStream);
            if (reader.TryGet("default_image", out DataObject? defaultImageObject) &&
                LoadTwitchImage(defaultImageObject!, out TwitchImage? defaultImage) &&
                reader.TryGet("id", out string? id) &&
                reader.TryGet("title", out string? title) &&
                reader.TryGet("prompt", out string? prompt) &&
                reader.TryGet("background_color", out string? backgroundColor) &&
                reader.TryGet("cost", out int cost) &&
                reader.TryGet("is_user_input_required", out bool isUserInputRequired) &&
                reader.TryGet("is_enabled", out bool isEnabled) &&
                reader.TryGet("is_paused", out bool isPaused) &&
                reader.TryGet("is_in_stock", out bool isInStock) &&
                reader.TryGet("should_redemptions_skip_request_queue", out bool shouldRedemptionsSkipRequestQueue))
            {
                return new(defaultImage!,
                    image,
                    cooldownExpiresAt,
                    id!,
                    title!,
                    prompt!,
                    backgroundColor!,
                    globalCooldownSeconds,
                    maxPerStream,
                    maxPerUserPerStream,
                    cost,
                    redemptionsRedeemedCurrentStream ?? 0,
                    isUserInputRequired,
                    isEnabled,
                    isPaused,
                    isInStock,
                    shouldRedemptionsSkipRequestQueue);
            }
            return null;
        }

        public List<TwitchRewardInfo> GetChannelRewards()
        {
            if (m_SelfUserInfo == null)
                return [];
            Response response = SendRequest(Request.MethodType.GET, $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={m_SelfUserInfo.ID}", m_UserAccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                List<DataObject> datas = responseJson.GetList<DataObject>("data");
                List<TwitchRewardInfo> rewards = [];
                foreach (DataObject data in datas)
                {
                    TwitchRewardInfo? reward = LoadTwitchRemoteInfo(data);
                    if (reward != null)
                        rewards.Add(reward);
                }
                return rewards;
            }
            return [];
        }

        public bool DeleteChannelReward(string rewardID)
        {
            if (m_SelfUserInfo == null)
                return false;
            Response response = SendRequest(Request.MethodType.DELETE, $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={m_SelfUserInfo.ID}&id={rewardID}", m_UserAccessToken);
            return response.StatusCode == 204;
        }

        public TwitchRewardInfo? CreateChannelReward(TwitchNewRewardInfo newRewardInfo)
        {
            if (m_SelfUserInfo == null)
                return null;

            DataObject obj = new()
            {
                ["title"] = newRewardInfo.m_Title,
                ["cost"] = newRewardInfo.m_Cost,
                ["is_enabled"] = newRewardInfo.m_IsEnabled,
                ["should_redemptions_skip_request_queue"] = newRewardInfo.m_ShouldRedemptionsSkipRequestQueue
            };

            if (newRewardInfo.m_BackgroundColor != null)
                obj["background_color"] = newRewardInfo.m_BackgroundColor;

            if (newRewardInfo.m_IsUserInputRequired)
            {
                obj["is_user_input_required"] = true;
                obj["prompt"] = newRewardInfo.m_Prompt;
            }

            if (newRewardInfo.m_MaxPerStream != -1)
            {
                obj["is_max_per_stream_enabled"] = true;
                obj["max_per_stream"] = newRewardInfo.m_MaxPerStream;
            }

            if (newRewardInfo.m_MaxPerUserPerStream != -1)
            {
                obj["is_max_per_user_per_stream_enabled"] = true;
                obj["max_per_user_per_stream"] = newRewardInfo.m_MaxPerUserPerStream;
            }

            if (newRewardInfo.m_GlobalCooldownSeconds != -1)
            {
                obj["is_global_cooldown_enabled"] = true;
                obj["global_cooldown_seconds"] = newRewardInfo.m_GlobalCooldownSeconds;
            }

            Response response = SendRequest(Request.MethodType.POST, $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={m_SelfUserInfo.ID}", obj, m_UserAccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                return LoadTwitchRemoteInfo(responseJson.GetList<DataObject>("data")[0]);
            }
            return null;
        }

        public TwitchRewardInfo? UpdateChannelReward(string rewardID, TwitchNewRewardInfo newRewardInfo)
        {
            if (m_SelfUserInfo == null)
                return null;

            DataObject obj = new()
            {
                ["title"] = newRewardInfo.m_Title,
                ["cost"] = newRewardInfo.m_Cost,
                ["is_enabled"] = newRewardInfo.m_IsEnabled,
                ["should_redemptions_skip_request_queue"] = newRewardInfo.m_ShouldRedemptionsSkipRequestQueue
            };

            if (newRewardInfo.m_BackgroundColor != null)
                obj["background_color"] = newRewardInfo.m_BackgroundColor;

            if (newRewardInfo.m_IsUserInputRequired)
            {
                obj["is_user_input_required"] = true;
                obj["prompt"] = newRewardInfo.m_Prompt;
            }

            if (newRewardInfo.m_MaxPerStream != -1)
            {
                obj["is_max_per_stream_enabled"] = true;
                obj["max_per_stream"] = newRewardInfo.m_MaxPerStream;
            }

            if (newRewardInfo.m_MaxPerUserPerStream != -1)
            {
                obj["is_max_per_user_per_stream_enabled"] = true;
                obj["max_per_user_per_stream"] = newRewardInfo.m_MaxPerUserPerStream;
            }

            if (newRewardInfo.m_GlobalCooldownSeconds != -1)
            {
                obj["is_global_cooldown_enabled"] = true;
                obj["global_cooldown_seconds"] = newRewardInfo.m_GlobalCooldownSeconds;
            }

            Response response = SendRequest(Request.MethodType.PATCH, $"https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={m_SelfUserInfo.ID}&id={rewardID}", obj, m_UserAccessToken);
            if (response.StatusCode == 200)
            {
                DataObject responseJson = JsonParser.Parse(response.Body);
                return LoadTwitchRemoteInfo(responseJson.GetList<DataObject>("data")[0]);
            }
            return null;
        }

        public bool UpdateRewardRedemption(string redemptionID, string rewardID, bool fullfilled)
        {
            if (m_SelfUserInfo == null)
                return false;
            DataObject obj = new() { ["status"] = (fullfilled) ? "FULFILLED" : "CANCELED" };
            Response response = SendRequest(Request.MethodType.PATCH, $"https://api.twitch.tv/helix/channel_points/custom_rewards/redemptions?broadcaster_id={m_SelfUserInfo.ID}&id={redemptionID}&reward_id={rewardID}", obj, m_UserAccessToken);
            return (response.StatusCode == 200);
        }
    }
}
