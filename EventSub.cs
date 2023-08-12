﻿using CorpseLib.Json;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.OAuth;
using TwitchCorpse;
using CorpseLib.Web.Http;
using CorpseLib;

namespace StreamGlass.Twitch
{
    public class EventSub : WebSocketProtocol
    {
        public class Metadata
        {
            private readonly string m_ID;
            private readonly string m_Type;
            private readonly string m_Timestamp;
            private readonly string m_Subscription;
            private readonly string m_Version;

            public string ID => m_ID;
            public string Type => m_Type;
            public string Timestamp => m_Timestamp;
            public string Subscription => m_Subscription;
            public string Version => m_Version;

            public Metadata(JObject obj)
            {
                m_ID = obj.Get<string>("message_id")!;
                m_Type = obj.Get<string>("message_type")!;
                m_Timestamp = obj.Get<string>("message_timestamp")!;
                m_Subscription = obj.GetOrDefault("subscription_type", "")!;
                m_Version = obj.GetOrDefault("subscription_version", "")!;
            }
        }

        public class Transport
        {
            private readonly string m_Method;
            private readonly string m_SessionID;
            private readonly string m_Callback;
            private readonly string m_Secret;

            public string Method => m_Method;
            public string SessionID => m_SessionID;
            public string Callback => m_Callback;
            public string Secret => m_Secret;

            public Transport(JObject obj)
            {
                m_Method = obj.Get<string>("method")!;
                if (m_Method == "websocket")
                {
                    m_Callback = "";
                    m_Secret = "";
                    m_SessionID = obj.Get<string>("session_id")!;
                }
                else
                {
                    m_SessionID = "";
                    m_Callback = obj.Get<string>("callback")!;
                    m_Secret = obj.GetOrDefault("secret", "")!;
                }
            }
        }

        public class Subscription
        {
            private readonly Dictionary<string, string> m_Conditions = new();
            private readonly Transport m_Transport;
            private readonly string m_ID;
            private readonly string m_Type;
            private readonly string m_Version;
            private readonly string m_Status;
            private readonly string m_CreatedAt;
            private readonly int m_Cost;

            public Transport Transport => m_Transport;
            public string ID => m_ID;
            public string Type => m_Type;
            public string Version => m_Version;
            public string Status => m_Status;
            public string CreatedAt => m_CreatedAt;
            public int Cost => m_Cost;

            public Subscription(JObject obj)
            {
                m_ID = obj.Get<string>("id")!;
                m_Type = obj.Get<string>("type")!;
                m_Version = obj.Get<string>("version")!;
                m_Status = obj.Get<string>("status")!;
                m_Cost = obj.Get<int>("cost")!;
                m_CreatedAt = obj.Get<string>("created_at")!;
                m_Transport = new(obj.Get<JObject>("transport")!);
                JObject conditionObject = obj.Get<JObject>("condition")!;
                foreach (var pair in conditionObject)
                    m_Conditions[pair.Key] = pair.Value.ToString();
            }

            public bool HaveCondition(string condition) => m_Conditions.ContainsKey(condition);
            public string GetCondition(string condition) => m_Conditions[condition];
        }

        public class EventData
        {
            private readonly JObject m_Data;

            public EventData(JObject data) { m_Data = data; }

            public User? GetUser(string user = "")
            {
                if (m_Data.TryGet(string.Format("{0}user_id", user), out string? id) &&
                    m_Data.TryGet(string.Format("{0}user_login", user), out string? login) &&
                    m_Data.TryGet(string.Format("{0}user_name", user), out string? name))
                    return new(id!, login!, name!, User.Type.NONE);
                return null;
            }

            public T GetOrDefault<T>(string key, T defaultValue) => m_Data.GetOrDefault(key, defaultValue)!;

            public bool TryGet<T>(string key, out T? ret) => m_Data.TryGet(key, out ret);
        }

        private readonly ITwitchHandler? m_TwitchHandler;
        private readonly RefreshToken m_Token;
        private readonly HashSet<string> m_TreatedMessage = new();
        private readonly HashSet<string> m_Subscriptions = new();
        private string m_ChannelID = "";

        public static EventSub NewConnection(RefreshToken token, ITwitchHandler twitchHandler)
        {
            EventSub protocol = new(token, twitchHandler);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        public static EventSub NewConnection(RefreshToken token)
        {
            EventSub protocol = new(token, null);
            TCPAsyncClient twitchIRCClient = new(protocol, URI.Parse("wss://irc-ws.chat.twitch.tv:443"));
            twitchIRCClient.Start();
            return protocol;
        }

        public EventSub(RefreshToken token, ITwitchHandler? twitchHandler): base(new Dictionary<string, string>() { { "Authorization", string.Format("Bearer {0}", token!.AccessToken) } })
        {
            m_Token = token;
            m_TwitchHandler = twitchHandler;
        }

        public void Connect(string channelID)
        {
            m_ChannelID = channelID;
            if (m_Token != null)
                SetExtension("Authorization", string.Format("Bearer {0}", m_Token!.AccessToken));
            Connect();
        }

        internal void Reconnect()
        {
            Disconnect();
            if (m_Token != null)
                SetExtension("Authorization", string.Format("Bearer {0}", m_Token!.AccessToken));
            Connect();
        }

        private bool RegisterSubscription(string sessionID, string subscriptionName, int subscriptionVersion, params string[] conditions)
        {
            if (m_Token == null)
                return false;
            JObject transportJson = new();
            transportJson.Set("method", "websocket");
            transportJson.Set("session_id", sessionID);
            JObject conditionJson = new();
            foreach (string condition in conditions)
                conditionJson.Set(condition, m_ChannelID);
            JObject message = new();
            message.Set("type", subscriptionName);
            message.Set("version", subscriptionVersion);
            message.Set("condition", conditionJson);
            message.Set("transport", transportJson);
            URLRequest request = new(URI.Parse("https://api.twitch.tv/helix/eventsub/subscriptions"), Request.MethodType.POST, message.ToNetworkString());
            request.AddContentType(MIME.APPLICATION.JSON);
            if (m_Token != null)
            {
                request.AddHeaderField("Authorization", string.Format("Bearer {0}", m_Token.AccessToken));
                request.AddHeaderField("Client-Id", m_Token.ClientID);
            }
            Response? response = request.Send();
            if (response != null && response.StatusCode == 202)
            {
                m_Subscriptions.Add(subscriptionName);
                return true;
            }
            else
                return false;
        }

        private void HandleWelcome(JObject payload)
        {
            if (payload.TryGet("session", out JObject? sessionObj))
            {
                if (sessionObj!.TryGet("id", out string? sessionID))
                {
                    if (!RegisterSubscription(sessionID!, "channel.follow", 2, "broadcaster_user_id", "moderator_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "channel.subscribe", 1, "broadcaster_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "channel.subscription.gift", 1, "broadcaster_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "channel.raid", 1, "to_broadcaster_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "channel.raid", 1, "from_broadcaster_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "channel.channel_points_custom_reward_redemption.add", 1, "broadcaster_user_id"))
                        return;
                    if (!RegisterSubscription(sessionID!, "stream.online", 1, "broadcaster_user_id"))
                        return;
                    RegisterSubscription(sessionID!, "stream.offline", 1, "broadcaster_user_id");
                }
            }
        }

        private void HandleFollow(EventData data)
        {
            User? follower = data.GetUser();
            if (follower != null)
                m_TwitchHandler?.OnFollow(follower);
        }

        private void HandleSub(EventData data)
        {
            int followTier;
            User? follower = data.GetUser();
            if (follower != null && data.TryGet("tier", out string? tier))
            {
                bool isGift = data.GetOrDefault("is_gift", false);
                switch (tier!)
                {
                    case "1000": followTier = 1; break;
                    case "2000": followTier = 2; break;
                    case "3000": followTier = 3; break;
                    default: return;
                }
                m_TwitchHandler?.OnSub(follower, followTier, isGift);
            }
        }

        private void HandleSubGift(EventData data)
        {
            int followTier;
            if (data.TryGet("is_anonymous", out bool? isAnonymous))
            {
                User? follower = data.GetUser();
                if (((bool)isAnonymous! || follower != null) && data.TryGet("tier", out string? tier) && data.TryGet("total", out int? nbGift))
                {
                    switch (tier!)
                    {
                        case "1000": followTier = 1; break;
                        case "2000": followTier = 2; break;
                        case "3000": followTier = 3; break;
                        default: return;
                    }
                    m_TwitchHandler?.OnGiftSub(follower, followTier, (int)nbGift!);
                }
            }
        }

        private void HandleReward(EventData data)
        {
            User? viewer = data.GetUser();
            if (viewer != null && data.TryGet("reward", out JObject? rewardInfo))
            {
                data.TryGet("user_input", out string? input);
                if (rewardInfo!.TryGet("title", out string? title))
                    m_TwitchHandler?.OnReward(viewer, title!, input ?? string.Empty);
            }
        }

        private void HandleRaid(EventData data, bool incomming)
        {
            User? from = data.GetUser("from_broadcaster_");
            User? to = data.GetUser("to_broadcaster_");
            if (from != null && to != null && data.TryGet("viewers", out int? viewers))
            {
                if (incomming)
                    m_TwitchHandler?.OnRaided(from, (int)viewers!);
                else
                    m_TwitchHandler?.OnRaiding(to, (int)viewers!);
            }
        }

        private void HandleStreamStart() => m_TwitchHandler?.OnStreamStart();

        private void HandleStreamStop() => m_TwitchHandler?.OnStreamStop();

        private static bool IsIncommingRaid(Subscription subscription) => !(subscription.HaveCondition("from_broadcaster_user_id") && !string.IsNullOrEmpty(subscription.GetCondition("from_broadcaster_user_id")));

        private void HandleNotification(JObject payload, string message)
        {
            if (payload.TryGet("subscription", out JObject? subscriptionObj) &&
                payload.TryGet("event", out JObject? eventObj))
            {
                Subscription subscription = new(subscriptionObj!);
                EventData eventData = new(eventObj!);
                switch (subscription.Type)
                {
                    case "channel.follow": HandleFollow(eventData); break;
                    case "channel.subscribe": HandleSub(eventData); break;
                    case "channel.subscription.gift": HandleSubGift(eventData); break;
                    case "channel.raid": HandleRaid(eventData, IsIncommingRaid(subscription)); break;
                    case "channel.channel_points_custom_reward_redemption.add": HandleReward(eventData); break;
                    case "stream.online": HandleStreamStart(); break;
                    case "stream.offline": HandleStreamStop(); break;
                    default: m_TwitchHandler?.UnhandledEventSub(message); break;
                }
            }
        }

        protected override void OnWSMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            JObject eventMessage = JObject.Parse(message);
            if (eventMessage.TryGet("metadata", out JObject? metadataObj) &&
                eventMessage.TryGet("payload", out JObject? payload))
            {
                Metadata metadata = new(metadataObj!);
                if (!m_TreatedMessage.Contains(metadata.ID))
                {
                    m_TreatedMessage.Add(metadata.ID);
                    switch (metadata.Type)
                    {
                        case "session_welcome": HandleWelcome(payload!); break;
                        case "session_keepalive": break;
                        case "notification": HandleNotification(payload!, message.Trim()); break;
                        case "session_reconnect": Reconnect(); break;
                        case "revocation": break;
                        default: m_TwitchHandler?.UnhandledEventSub(message.Trim()); break;
                    }
                }
            }
        }
    }
}