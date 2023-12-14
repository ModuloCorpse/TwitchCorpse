using CorpseLib;
using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.StructuredText;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using System.Text;
using System.Text.RegularExpressions;
using static TwitchCorpse.TwitchChatMessage;

namespace TwitchCorpse
{
    public partial class TwitchChat : WebSocketProtocol
    {
        public static readonly Logger TWITCH_IRC = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-TwitchIRC.log") };
        public static void StartLogging() => TWITCH_IRC.Start();
        public static void StopLogging() => TWITCH_IRC.Stop();

        private static readonly List<string> ms_Colors =
        [
            "#ff0000",
            "#00ff00",
            "#0000ff",
            "#b22222",
            "#ff7f50",
            "#9acd32",
            "#ff4500",
            "#2e8b57",
            "#daa520",
            "#d2691e",
            "#5f9ea0",
            "#1e90ff",
            "#ff69b4",
            "#8a2be2",
            "#00ff7f"
        ];

        private readonly Dictionary<string, TwitchUser> m_LoadedUser = [];
        private readonly ITwitchHandler? m_TwitchHandler;
        private readonly TwitchAPI m_API;
        private readonly TwitchUser? m_SelfUserInfo = null;
        private readonly RefreshToken? m_AccessToken = null;
        private readonly string m_Channel;
        private readonly string m_UserName = string.Empty;
        private string m_ChatColor = string.Empty;
        private TwitchImage.Theme.Type m_ChatTheme = TwitchImage.Theme.Type.DARK;

        public TwitchUser Self => m_SelfUserInfo!;

        public static TwitchChat NewConnection(TwitchAPI api, string channel, string username, RefreshToken token, ITwitchHandler twitchHandler)
        {
            TwitchChat protocol = new(api, channel, username, token, twitchHandler);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        public static TwitchChat NewConnection(TwitchAPI api, string channel, string username, RefreshToken token)
        {
            TwitchChat protocol = new(api, channel, username, token, null);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        private static TwitchCheermote.Tier? SearchCheermote(string str, ref int idx, TwitchCheermote[] cheermotes)
        {
            int i = idx;
            foreach (TwitchCheermote cheermote in cheermotes)
            {
                if (str.IndexOf(cheermote.Prefix, i, cheermote.Prefix.Length) == i)
                {
                    i += cheermote.Prefix.Length;

                    int cheer = 0;
                    while (i != str.Length && char.IsNumber(str[i]))
                    {
                        cheer = (cheer * 10) + (str[i] - '0');
                        ++i;
                    }

                    if (cheer == 0)
                        return null;

                    if (i == str.Length || char.IsWhiteSpace(str[i]))
                    {
                        TwitchCheermote.Tier? currentTier = null;
                        foreach (TwitchCheermote.Tier tier in cheermote.Tiers)
                        {
                            if (tier.CanCheer && cheer >= tier.Threshold)
                                currentTier = tier;
                        }
                        if (currentTier != null)
                            idx = i;
                        return currentTier;
                    }
                }
            }
            return null;
        }

        private bool TryAddAnimatedImageFromFormat(TwitchImage.Format format, Text text)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddAnimatedImage(format[scale]);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryAddImageFromFormat(TwitchImage.Format format, Text text)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddImage(format[scale]);
                        return true;
                    }
                }
            }
            return false;
        }

        private void AddTwitchImage(TwitchImage image, Text text)
        {
            if (!TryAddAnimatedImageFromFormat(image[m_ChatTheme][TwitchImage.Format.Type.ANIMATED], text))
            {
                if (!TryAddImageFromFormat(image[m_ChatTheme][TwitchImage.Format.Type.STATIC], text))
                {
                    TwitchImage.Theme.Type oppositeTheme = (m_ChatTheme == TwitchImage.Theme.Type.DARK) ? TwitchImage.Theme.Type.LIGHT : TwitchImage.Theme.Type.DARK;
                    if (!TryAddAnimatedImageFromFormat(image[oppositeTheme][TwitchImage.Format.Type.ANIMATED], text))
                    {
                        if (!TryAddImageFromFormat(image[oppositeTheme][TwitchImage.Format.Type.STATIC], text))
                            text.AddText(image.Alt);
                    }
                }
            }
        }

        private void AddTextToMessage(TwitchAPI api, Text text, string str, bool loadCheermotes)
        {
            if (!loadCheermotes)
            {
                text.AddText(str);
                return;
            }

            TwitchCheermote[] cheermotes = api.GetTwitchCheermotes();
            StringBuilder builder = new();
            for (int i = 0; i < str.Length; ++i)
            {
                if (i == 0 || str.CharEqual(i - 1, char.IsWhiteSpace))
                {
                    TwitchCheermote.Tier? cheermoteTier = SearchCheermote(str, ref i, cheermotes);
                    if (cheermoteTier != null)
                    {
                        text.AddText(builder.ToString());
                        builder.Clear();
                        AddTwitchImage(cheermoteTier.Image, text);
                    }
                    else
                        builder.Append(str[i]);
                }
                else
                    builder.Append(str[i]);
            }

            text.AddText(builder.ToString());
        }

        private Text Convert(TwitchAPI api, string message, List<SimpleEmote> emoteList, bool loadCheermotes)
        {
            Text ret = new();
            int lastIndex = 0;
            foreach (SimpleEmote emote in emoteList)
            {
                AddTextToMessage(api, ret, message[lastIndex..emote.Start], loadCheermotes);
                TwitchEmoteInfo? emoteInfo = api.GetEmoteFromID(emote.ID);
                if (emoteInfo != null)
                    AddTwitchImage(emoteInfo.Image, ret);
                else
                    ret.AddText(message[emote.Start..(emote.End + 1)]);
                lastIndex = emote.End + 1;
            }
            if (lastIndex < message.Length)
                AddTextToMessage(api, ret, message[lastIndex..message.Length], loadCheermotes);
            return ret;
        }

        private TwitchChat(TwitchAPI api, string channel, string username, RefreshToken token, ITwitchHandler? twitchHandler)
        {
            m_API = api;
            m_Channel = channel;
            m_UserName = username;
            m_AccessToken = token;
            m_AccessToken.Refreshed += (_) => SendAuth();
            m_SelfUserInfo = m_API.GetUserInfoOfToken(m_AccessToken);
            m_TwitchHandler = twitchHandler;
        }

        public void SetChatTheme(TwitchImage.Theme.Type theme) => m_ChatTheme = theme;

        private void SendAuth()
        {
            if (m_AccessToken == null)
                return;
            SendMessage(new TwitchChatMessage("CAP REQ", parameters: "twitch.tv/membership twitch.tv/tags twitch.tv/commands"));
            SendMessage(new TwitchChatMessage("PASS", channel: string.Format("oauth:{0}", m_AccessToken.AccessToken)));
            SendMessage(new TwitchChatMessage("NICK", channel: m_SelfUserInfo != null ? m_SelfUserInfo.DisplayName : m_UserName));
        }

        private static string GetUserMessageColor(string username, string color)
        {
            if (string.IsNullOrEmpty(color))
            {
                int colorIdx = 0;
                foreach (char c in username)
                    colorIdx += c;
                colorIdx %= ms_Colors.Count;
                return ms_Colors[colorIdx];
            }
            return color;
        }

        private TwitchUser LoadUser(TwitchChatMessage message, bool self)
        {
            string userID = message.GetTag("user-id");
            if (m_LoadedUser.TryGetValue(userID, out TwitchUser? user))
                return user;
            else
            {
                List<TwitchBadgeInfo> badges = [];
                foreach (string badge in message.GetBadges())
                {
                    string version = message.GetBadgeVersion(badge);
                    TwitchBadgeInfo? badgeInfo = m_API.GetBadge(badge, version);
                    if (badgeInfo != null)
                        badges.Add(badgeInfo);
                }
                string displayName = message.GetTag("display-name");
                TwitchUser.Type userType = m_API.GetUserType(self, message.GetTag("mod") == "1", message.GetTag("user-type"), userID);
                TwitchUser newUser = new(userID, message.Nick, displayName, string.Empty, userType, badges);
                m_LoadedUser[userID] = newUser;
                return newUser;
            }
        }

        private void CreateUserMessage(TwitchChatMessage message, bool highlight, bool self)
        {
            bool hasGivenBits = message.HaveTag("bits");
            TwitchUser user = LoadUser(message, self);
            Text displayableMessage = Convert(m_API, message.Parameters, message.Emotes, hasGivenBits);
            m_TwitchHandler?.OnChatMessage(user, highlight, message.GetTag("id"),
                GetUserMessageColor(user.DisplayName, self ? m_ChatColor : message.GetTag("color")), displayableMessage);
            if (hasGivenBits)
            {
                int bits = int.Parse(message.GetTag("bits"));
                m_TwitchHandler?.OnBits(user, bits, displayableMessage);
            }
        }

        private void TreatUserNotice(TwitchChatMessage message)
        {
            CreateUserMessage(message, true, false);
            if (message.HaveTag("msg-id"))
            {
                string noticeType = message.GetTag("msg-id");
                if (noticeType == "sub" || noticeType == "resub")
                {
                    if (message.HaveTag("msg-param-sub-plan") &&
                        message.HaveTag("msg-param-cumulative-months") &&
                        message.HaveTag("msg-param-should-share-streak"))
                    {
                        int followTier;
                        switch (message.GetTag("msg-param-sub-plan"))
                        {
                            case "1000": followTier = 1; break;
                            case "2000": followTier = 2; break;
                            case "3000": followTier = 3; break;
                            case "Prime": followTier = 4; break;
                            default: return;
                        }
                        TwitchUser user = LoadUser(message, false);
                        int cumulativeMonth = int.Parse(message.GetTag("msg-param-cumulative-months"));
                        bool shareStreakMonth = message.GetTag("msg-param-cumulative-months") == "1";
                        int streakMonth = message.HaveTag("msg-param-streak-months") ? int.Parse(message.GetTag("msg-param-streak-months")) : -1;
                        Text displayableMessage = Convert(m_API, message.Parameters, message.Emotes, false);
                        m_TwitchHandler?.OnSharedSub(user, followTier, cumulativeMonth, shareStreakMonth ? streakMonth : -1, displayableMessage);
                    }
                }
                else if (noticeType == "subgift")
                {
                    if (message.HaveTag("msg-param-sub-plan") &&
                        message.HaveTag("msg-param-gift-months") &&
                        message.HaveTag("msg-param-months") &&
                        message.HaveTag("msg-param-recipient-display-name") &&
                        message.HaveTag("msg-param-recipient-user-name"))
                    {
                        int followTier;
                        switch (message.GetTag("msg-param-sub-plan"))
                        {
                            case "1000": followTier = 1; break;
                            case "2000": followTier = 2; break;
                            case "3000": followTier = 3; break;
                            case "Prime": followTier = 4; break;
                            default: return;
                        }
                        TwitchUser user = LoadUser(message, false);

                        string userID = message.GetTag("user-id");
                        if (!m_LoadedUser.TryGetValue(userID, out TwitchUser? recipient))
                        {
                            string recipientName = message.GetTag("msg-param-recipient-user-name");
                            string recipientDisplayName = message.GetTag("msg-param-recipient-display-name");
                            if (string.IsNullOrEmpty(recipientDisplayName))
                                recipientDisplayName = recipientName;
                            string recipientID = message.GetTag("msg-param-recipient-id");
                            recipient = new(recipientID, recipientName, recipientDisplayName, string.Empty, TwitchUser.Type.NONE, []);
                        }

                        int monthGifted = int.Parse(message.GetTag("msg-param-gift-months"));
                        int cumulativeMonth = int.Parse(message.GetTag("msg-param-months"));
                        Text displayableMessage = Convert(m_API, message.Parameters, message.Emotes, false);
                        m_TwitchHandler?.OnSharedGiftSub(user, recipient, followTier, cumulativeMonth, monthGifted, displayableMessage);
                    }
                }
            }
        }

        private void LoadEmoteSets(TwitchChatMessage message)
        {
            if (message.HaveTag("emote-sets"))
            {
                string emoteSetsStr = message.GetTag("emote-sets");
                string[] emoteSetIDs = emoteSetsStr.Split(',');
                foreach (string emoteSetID in emoteSetIDs)
                    m_API.LoadEmoteSet(emoteSetID);
            }
        }

        private void TreatReceivedMessage(string data)
        {
            TwitchChatMessage? message = Parse(data, m_API);
            if (message != null)
            {
                switch (message.GetCommand().Name)
                {
                    case "PING":
                    {
                        SendMessage(new TwitchChatMessage("PONG", parameters: message.Parameters));
                        break;
                    }
                    case "USERSTATE":
                    {
                        LoadUser(message, false);
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        LoadEmoteSets(message);
                        break;
                    }
                    case "JOIN":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        if (m_SelfUserInfo?.Name != message.Nick)
                        {
                            TwitchUser? user = m_API.GetUserInfoFromLogin(message.Nick);
                            if (user != null)
                                m_TwitchHandler?.OnUserJoinChat(user);
                        }
                        else
                            m_TwitchHandler?.OnChatJoined();
                        break;
                    }
                    case "USERLIST":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        string[] users = message.Parameters.Split(' ');
                        foreach (string userLogin in users)
                        {
                            if (m_SelfUserInfo?.Name != userLogin)
                            {
                                TwitchUser? user = m_API.GetUserInfoFromLogin(userLogin);
                                if (user != null)
                                    m_TwitchHandler?.OnUserJoinChat(user);
                            }
                        }
                        break;
                    }
                    case "PRIVMSG":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        CreateUserMessage(message, false, false);
                        break;
                    }
                    case "USERNOTICE":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        TreatUserNotice(message);
                        break;
                    }
                    case "LOGGED":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        SendMessage(new TwitchChatMessage("JOIN", channel: string.Format("#{0}", m_Channel)));
                        break;
                    }
                    case "GLOBALUSERSTATE":
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        LoadEmoteSets(message);
                        m_ChatColor = message.GetTag("color");
                        break;
                    }
                    case "CLEARCHAT":
                    {
                        if (message.HaveTag("target-user-id"))
                            m_TwitchHandler?.OnChatUserRemoved(message.GetTag("target-user-id"));
                        else
                            m_TwitchHandler?.OnChatClear();
                        break;
                    }
                    case "CLEARMSG":
                    {
                        if (message.HaveTag("target-msg-id"))
                            m_TwitchHandler?.OnChatMessageRemoved(message.GetTag("target-msg-id"));
                        break;
                    }
                    case "RECONNECT":
                    {
                        Reconnect();
                        break;
                    }
                    default:
                    {
                        TWITCH_IRC.Log(string.Format("<= {0}", data));
                        break;
                    }
                }
            }
        }

        internal void TreatReceivedBuffer(string dataBuffer)
        {
            int position;
            do
            {
                position = dataBuffer.IndexOf("\r\n");
                if (position >= 0)
                {
                    string data = dataBuffer[..position];
                    dataBuffer = dataBuffer[(position + 2)..];
                    TreatReceivedMessage(data);
                }
            } while (position >= 0);
        }

        public void TestMessage(string message) => TreatReceivedMessage(message);
        public void TestMessages(IEnumerable<string> messages)
        {
            foreach (string message in messages)
            {
                TreatReceivedMessage(message);
                Thread.Sleep(100);
            }
        }

        public void SendMessage(string message)
        {
            Tags tags = new();
            tags.AddTag("display-name", m_SelfUserInfo?.DisplayName ?? m_UserName);
            if (m_SelfUserInfo != null)
            {
                tags.AddTag("user-id", m_SelfUserInfo.ID);
            }
            TwitchChatMessage messageToSend = new("PRIVMSG", channel: string.Format("#{0}", m_Channel), parameters: message);
            SendMessage(messageToSend);
            CreateUserMessage(messageToSend, false, true);
        }

        private void SendMessage(TwitchChatMessage message)
        {
            try
            {
                string messageData = message.ToString();
                if (!messageData.StartsWith("PONG"))
                    TWITCH_IRC.Log(string.Format("=> {0}", OAuthRegex().Replace(messageData.Trim(), "oauth:*****")));
                Send(messageData);
            }
            catch (Exception e)
            {
                TWITCH_IRC.Log(string.Format("On send exception: {0}", e));
            }
        }

        protected override void OnWSOpen(Response _) => SendAuth();

        protected override void OnWSMessage(string message) => TreatReceivedBuffer(message);
        protected override void OnWSClose(int code, string closeMessage) => TWITCH_IRC.Log(string.Format("<=[Error] WebSocket closed ({0}): {1}", code, closeMessage));
        
        [GeneratedRegex("(oauth:).+")]
        private static partial Regex OAuthRegex();
    }
}
