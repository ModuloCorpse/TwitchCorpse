﻿using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;

namespace TwitchCorpse
{
    public class TwitchAPI
    {
        public static readonly Logger TWITCH_API = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-TwitchAPI.log") };
        public static void StartLogging() => TWITCH_API.Start();
        public static void StopLogging() => TWITCH_API.Stop();

        private readonly Dictionary<string, Dictionary<string, TwitchBadgeInfo>> m_CachedBadges = [];
        private readonly Dictionary<string, TwitchEmoteInfo> m_CachedEmotes = [];
        private readonly Dictionary<string, TwitchUser> m_CachedUserInfoFromLogin = [];
        private readonly Dictionary<string, TwitchUser> m_CachedUserInfoFromID = [];
        private readonly HashSet<string> m_LoadedChannelIDEmoteSets = [];
        private readonly HashSet<string> m_LoadedEmoteSets = [];
        private readonly RefreshToken m_AccessToken;
        private readonly TwitchUser m_SelfUserInfo = new(TwitchUser.Type.BROADCASTER);
        private string m_EmoteURLTemplate = string.Empty;

        public TwitchAPI(RefreshToken accessToken)
        {
            m_AccessToken = accessToken;
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", accessToken);
            if (response.StatusCode == 200)
            {
                TwitchUser? user = GetUserInfo(response.Body, TwitchUser.Type.BROADCASTER);
                if (user != null)
                {
                    m_SelfUserInfo = user;
                    LoadChannelEmoteSet(user);
                }
            }
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

        private static Response SendRequest(Request.MethodType method, string url, JFile content, RefreshToken? token)
        {
            URLRequest request = new(URI.Parse(url), method, content.ToNetworkString());
            request.AddContentType(MIME.APPLICATION.JSON);
            return SendComposedRequest(request, token);
        }

        private static Response SendRequest(Request.MethodType method, string url, RefreshToken? token) => SendComposedRequest(new(URI.Parse(url), method), token);

        private JFile LoadBadgeContent(string content)
        {
            JFile responseJson = new(content);
            List<JObject> datas = responseJson.GetList<JObject>("data");
            foreach (JObject data in datas)
            {
                if (data.TryGet("set_id", out string? setID))
                {
                    List<JObject> versions = data.GetList<JObject>("versions");
                    foreach (JObject version in versions)
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
                            if (!m_CachedBadges.ContainsKey(setID!))
                                m_CachedBadges[setID!] = [];
                            m_CachedBadges[setID!][badgeInfo.ID] = badgeInfo;
                        }
                    }
                }
            }
            return responseJson;
        }

        public void LoadGlobalChatBadges()
        {
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/badges/global", m_AccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
        }

        public void LoadChannelChatBadges(TwitchUser user)
        {
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/badges?broadcaster_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
                LoadBadgeContent(response.Body);
        }

        public TwitchBadgeInfo? GetBadge(string badge, string id)
        {
            if (m_CachedBadges.TryGetValue(badge, out Dictionary<string, TwitchBadgeInfo>? badgesInfo))
            {
                if (badgesInfo.TryGetValue(id, out TwitchBadgeInfo? badgeInfo))
                    return badgeInfo;
            }
            return null;
        }

        private JFile LoadEmoteSetContent(string content)
        {
            JFile responseJson = new(content);
            if (responseJson.TryGet("template", out string? template))
            {
                List<JObject> datas = responseJson.GetList<JObject>("data");
                foreach (JObject data in datas)
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
                            TwitchImage.Theme theme = (themeStr == "dark") ? image[TwitchImage.Theme.Type.DARK] : image[TwitchImage.Theme.Type.LIGHT];
                            foreach (string formatStr in formats)
                            {
                                TwitchImage.Format format = (formatStr == "animated") ? theme[TwitchImage.Format.Type.ANIMATED] : theme[TwitchImage.Format.Type.STATIC];
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
                        m_CachedEmotes[info.ID] = info;
                    }
                }
            }
            return responseJson;
        }

        public void LoadGlobalEmoteSet()
        {
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/chat/emotes/global", m_AccessToken);
            if (response.StatusCode == 200)
            {
                JFile responseJson = LoadEmoteSetContent(response.Body);
                if (responseJson.TryGet("template", out string? template))
                    m_EmoteURLTemplate = template!;
            }
        }

        public void LoadChannelEmoteSet(TwitchUser user)
        {
            if (m_LoadedChannelIDEmoteSets.Contains(user.ID))
                return;
            m_LoadedChannelIDEmoteSets.Add(user.ID);
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/emotes?broadcaster_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
                LoadEmoteSetContent(response.Body);
        }

        public void LoadEmoteSet(string emoteSetID)
        {
            if (m_LoadedEmoteSets.Contains(emoteSetID))
                return;
            m_LoadedEmoteSets.Add(emoteSetID);
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/chat/emotes/set?emote_set_id={0}", emoteSetID), m_AccessToken);
            if (response.StatusCode == 200)
                LoadEmoteSetContent(response.Body);
        }

        public void LoadEmoteSetFromFollowedChannel(TwitchUser user)
        {
            List<TwitchUser> followedBy = GetChannelFollowedByID(user);
            foreach (TwitchUser followed in followedBy)
                LoadChannelEmoteSet(followed);
        }

        public string GetEmoteURL(string id, bool isAmimated, byte scale, bool isDarkMode)
        {
            TwitchEmoteInfo? emoteInfo = (m_CachedEmotes.TryGetValue(id, out TwitchEmoteInfo? info)) ? info : null; ;
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

        public TwitchEmoteInfo? GetEmoteFromID(string id)
        {
            TwitchEmoteInfo? emoteInfo = (m_CachedEmotes.TryGetValue(id, out TwitchEmoteInfo? info)) ? info : null; ;
            if (emoteInfo != null)
                return emoteInfo;
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
                    JFile json = new(response.Body);
                    isMod = json.GetList<JObject>("data").Count != 0;
                }
            }
            return GetUserType(self, isMod, type, id);
        }

        private TwitchUser? GetUserInfo(string content, TwitchUser.Type? userType)
        {
            JFile responseJson = new(content);
            List<JObject> datas = responseJson.GetList<JObject>("data");
            if (datas.Count > 0)
            {
                JObject data = datas[0];
                if (data.TryGet("id", out string? id) &&
                    data.TryGet("login", out string? login) &&
                    data.TryGet("display_name", out string? displayName) &&
                    data.TryGet("type", out string? type) &&
                    data.TryGet("profile_image_url", out string? profileImageURL))
                    return new(id!, login!, displayName!, profileImageURL!, (userType != null) ? (TwitchUser.Type)userType! : GetUserType(false, type!, id!), []);
            }
            return null;
        }

        public TwitchUser? GetUserInfoOfToken(RefreshToken token)
        {
            Response response = SendRequest(Request.MethodType.GET, "https://api.twitch.tv/helix/users", token);
            if (response.StatusCode == 200)
                return GetUserInfo(response.Body, TwitchUser.Type.SELF);
            return null;
        }

        public TwitchUser GetSelfUserInfo() => m_SelfUserInfo;

        public TwitchUser? GetUserInfoFromLogin(string login)
        {
            TwitchUser? userInfo = (m_CachedUserInfoFromLogin.TryGetValue(login, out TwitchUser? info)) ? info : null; ;
            if (userInfo != null)
                return userInfo;
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/users?login={0}", login), m_AccessToken);
            if (response.StatusCode == 200)
            {
                TwitchUser? ret = GetUserInfo(response.Body, null);
                if (ret != null)
                {
                    m_CachedUserInfoFromID[ret.ID] = ret;
                    m_CachedUserInfoFromLogin[ret.Name] = ret;
                }
                return ret;
            }
            return null;
        }

        public string GetUserProfilePictureFromLogin(string login) => GetUserInfoFromLogin(login)?.ProfileImageURL ?? string.Empty;

        public TwitchChannelInfo? GetChannelInfo(string login)
        {
            TwitchChannelInfo? channelInfo = null;
            if (channelInfo != null)
                return channelInfo;
            TwitchUser? broadcasterInfo = GetUserInfoFromLogin(login);
            if (broadcasterInfo != null)
            {
                Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/channels?broadcaster_id={0}", broadcasterInfo.ID), m_AccessToken);
                if (response.StatusCode == 200)
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
                            return new TwitchChannelInfo(broadcasterInfo, gameID!, gameName!, title!, language!);
                    }
                }
            }
            return null;
        }

        public bool SetChannelInfo(TwitchUser user, string title, string gameID, string language = "")
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(gameID) && string.IsNullOrEmpty(language))
                return false;
            JFile body = [];
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
                JFile responseJson = new(response.Body);
                foreach (JObject data in responseJson.GetList<JObject>("data"))
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
            JFile json = new()
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
            JFile json = [];
            JFile data = new()
            {
                { "user_id", user.ID }
            };
            if (duration > 0)
                data.Add("duration", duration);
            data.Add("reason", reason);
            json.Add("data", data);
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

        public List<TwitchStreamInfo> GetStreamInfoByID(TwitchUser user)
        {
            List<TwitchStreamInfo> ret = [];
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/streams?user_id={0}", user.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                JFile responseJson = new(response.Body);
                foreach (JObject data in responseJson.GetList<JObject>("data"))
                {
                    if (data.TryGet("id", out string? id) &&
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
                        ret.Add(new(new(userID!, userLogin!, userName!, string.Empty, GetUserType(false, string.Empty, userID!), []), data.GetList<string>("tags"), id!, gameID!, gameName!, title!, language!, thumbnailURL!, (int)viewerCount!, (bool)isMature!));
                }
            }
            return ret;
        }

        public bool StartCommercial(uint duration)
        {
            if (m_SelfUserInfo == null)
                return false;
            JFile json = new()
            {
                { "broadcaster_id", m_SelfUserInfo.ID },
                { "length", duration }
            };
            Response response = SendRequest(Request.MethodType.POST, "https://api.twitch.tv/helix/channels/commercial", json, m_AccessToken);
            return response.StatusCode == 200;
        }

        public static void LoadCheermoteFormat(JObject obj, TwitchImage.Theme theme, TwitchImage.Format.Type formatType)
        {
            TwitchImage.Format format = theme[formatType];
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

        public static void LoadCheermoteTheme(JObject obj, TwitchImage image, TwitchImage.Theme.Type themeType)
        {
            TwitchImage.Theme theme = image[themeType];
            if (obj.TryGet("animated", out JObject? animated))
                LoadCheermoteFormat(animated!, theme, TwitchImage.Format.Type.ANIMATED);
            if (obj.TryGet("static", out JObject? @static))
                LoadCheermoteFormat(@static!, theme, TwitchImage.Format.Type.STATIC);
        }

        public TwitchCheermote[] GetTwitchCheermotes()
        {
            List<TwitchCheermote> ret = [];
            Response response = SendRequest(Request.MethodType.GET, string.Format("https://api.twitch.tv/helix/bits/cheermotes?broadcaster_id={0}", m_SelfUserInfo.ID), m_AccessToken);
            if (response.StatusCode == 200)
            {
                JFile responseJson = new(response.Body);
                foreach (JObject data in responseJson.GetList<JObject>("data"))
                {
                    if (data.TryGet("prefix", out string? prefix))
                    {
                        TwitchCheermote twitchCheermote = new(prefix!);
                        foreach (JObject tier in data.GetList<JObject>("tiers"))
                        {
                            if (tier.TryGet("min_bits", out int? threshold) &&
                                tier.TryGet("can_cheer", out bool? canCheer) &&
                                tier.TryGet("images", out JObject? images))
                            {
                                TwitchImage image = new(string.Format("{0}{1}", prefix!, threshold));
                                if (images!.TryGet("dark", out JObject? dark))
                                    LoadCheermoteTheme(dark!, image, TwitchImage.Theme.Type.DARK);
                                if (images!.TryGet("light", out JObject? light))
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
    }
}
