using CorpseLib.Network;
using CorpseLib.StructuredText;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;

namespace TwitchCorpse.IRC
{
    public class TwitchIRCProtocol: WebSocketProtocol
    {
        private static readonly List<string> ms_Colors = new()
        {
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
        };

        private readonly ITwitchHandler? m_TwitchHandler;
        private readonly API m_API;
        private readonly User? m_SelfUserInfo = null;
        private readonly RefreshToken? m_AccessToken = null;
        private readonly string m_Channel;
        private readonly string m_UserName = string.Empty;
        private string m_ChatColor = string.Empty;

        public static TwitchIRCProtocol NewConnection(API api, string channel, string username, RefreshToken token, ITwitchHandler twitchHandler)
        {
            TwitchIRCProtocol protocol = new(api, channel, username, token, twitchHandler);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        public static TwitchIRCProtocol NewConnection(API api, string channel, string username, RefreshToken token)
        {
            TwitchIRCProtocol protocol = new(api, channel, username, token, null);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        private TwitchIRCProtocol(API api, string channel, string username, RefreshToken token, ITwitchHandler? twitchHandler)
        {
            m_API = api;
            m_Channel = channel;
            m_UserName = username;
            m_AccessToken = token;
            m_AccessToken.Refreshed += (_) => SendAuth();
            m_SelfUserInfo = m_API.GetUserInfoOfToken(m_AccessToken);
            m_TwitchHandler = twitchHandler;
        }

        private void SendAuth()
        {
            if (m_AccessToken == null)
                return;
            SendMessage(new Message("CAP REQ", parameters: "twitch.tv/membership twitch.tv/tags twitch.tv/commands"));
            SendMessage(new Message("PASS", channel: string.Format("oauth:{0}", m_AccessToken.AccessToken)));
            SendMessage(new Message("NICK", channel: (m_SelfUserInfo != null) ? m_SelfUserInfo.DisplayName : m_UserName));
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

        private void CreateUserMessage(Message message, bool highlight, bool self)
        {
            string displayName;
            if (self)
            {
                if (m_SelfUserInfo != null)
                    displayName = m_SelfUserInfo.DisplayName;
                else
                    displayName = "StreamGlass";
            }
            else
                displayName = message.GetTag("display-name");
            string userID = message.GetTag("user-id");
            User.Type userType = m_API.GetUserType(self, message.GetTag("mod") == "1", message.GetTag("user-type"), userID);
            User user = new(userID, message.Nick, displayName, userType);
            Text displayableMessage = Helper.Convert(m_API, message.Parameters, message.Emotes);
            m_TwitchHandler?.OnChatMessage(user, highlight, message.GetTag("id"),
                GetUserMessageColor(displayName, (self) ? m_ChatColor : message.GetTag("color")), displayableMessage);
            if (message.HaveTag("bits"))
            {
                int bits = int.Parse(message.GetTag("bits"));
                m_TwitchHandler?.OnBits(user, bits, displayableMessage);
            }
        }

        private void TreatUserNotice(Message message)
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
                        string displayName = message.GetTag("display-name");
                        if (string.IsNullOrEmpty(displayName))
                            displayName = message.Nick;
                        int followTier;
                        switch (message.GetTag("msg-param-sub-plan"))
                        {
                            case "1000": followTier = 1; break;
                            case "2000": followTier = 2; break;
                            case "3000": followTier = 3; break;
                            case "Prime": followTier = 4; break;
                            default: return;
                        }
                        string userID = message.GetTag("user-id");
                        User.Type userType = m_API.GetUserType(false, message.GetTag("mod") == "1", message.GetTag("user-type"), userID);
                        User user = new(userID, message.Nick, displayName, userType);
                        int cumulativeMonth = int.Parse(message.GetTag("msg-param-cumulative-months"));
                        bool shareStreakMonth = message.GetTag("msg-param-cumulative-months") == "1";
                        int streakMonth = (message.HaveTag("msg-param-streak-months")) ? int.Parse(message.GetTag("msg-param-streak-months")) : -1;
                        Text displayableMessage = Helper.Convert(m_API, message.Parameters, message.Emotes);
                        m_TwitchHandler?.OnSharedSub(user, followTier, cumulativeMonth, (shareStreakMonth) ? streakMonth : -1, displayableMessage);
                    }
                }
            }
        }

        private void LoadEmoteSets(Message message)
        {
            if (message.HaveTag("emote-sets"))
            {
                string emoteSetsStr = message.GetTag("emote-sets");
                string[] emoteSetIDs = emoteSetsStr.Split(',');
                foreach (string emoteSetID in emoteSetIDs)
                    m_API.LoadEmoteSet(emoteSetID);
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
                    Message? message = Message.Parse(data, m_API);
                    if (message != null)
                    {
                        switch (message.GetCommand().Name)
                        {
                            case "PING":
                            {
                                SendMessage(new Message("PONG", parameters: message.Parameters));
                                break;
                            }
                            case "USERSTATE":
                            {
                                LoadEmoteSets(message);
                                m_TwitchHandler?.OnChatJoined();
                                break;
                            }
                            case "JOIN":
                            {
                                if (m_SelfUserInfo?.Name != message.Nick)
                                {
                                    User? user = m_API.GetUserInfoFromLogin(message.Nick);
                                    if (user != null)
                                        m_TwitchHandler?.OnUserJoinChat(user);
                                }
                                break;
                            }
                            case "USERLIST":
                            {
                                string[] users = message.Parameters.Split(' ');
                                foreach (string userLogin in users)
                                {
                                    if (m_SelfUserInfo?.Name != userLogin)
                                    {
                                        User? user = m_API.GetUserInfoFromLogin(userLogin);
                                        if (user != null)
                                            m_TwitchHandler?.OnUserJoinChat(user);
                                    }
                                }
                                break;
                            }
                            case "PRIVMSG":
                            {
                                CreateUserMessage(message, false, false);
                                break;
                            }
                            case "USERNOTICE":
                            {
                                TreatUserNotice(message);
                                break;
                            }
                            case "LOGGED":
                            {
                                SendMessage(new Message("JOIN", channel: string.Format("#{0}", m_Channel)));
                                break;
                            }
                            case "GLOBALUSERSTATE":
                            {
                                LoadEmoteSets(message);
                                m_ChatColor = message.GetTag("color");
                                break;
                            }
                            default:
                            {
                                break;
                            }
                        }
                    }
                }
            } while (position >= 0);
        }

        public void SendMessage(string message)
        {
            Message messageToSend = new("PRIVMSG", channel: string.Format("#{0}", m_Channel), parameters: message);
            SendMessage(messageToSend);
            CreateUserMessage(messageToSend, false, true);
        }

        private void SendMessage(Message message)
        {
            try
            {
                string messageData = message.ToString();
                Send(messageData);
            }
            catch {}
        }

        protected override void OnWSOpen(Response _) => SendAuth();

        protected override void OnWSMessage(string message) => TreatReceivedBuffer(message);
    }
}
