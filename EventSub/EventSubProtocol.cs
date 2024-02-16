using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using TwitchCorpse.EventSub.Core;
using TwitchCorpse.EventSub.Subscriptions;
using static TwitchCorpse.TwitchEventSub;

namespace TwitchCorpse.EventSub
{
    internal class EventSubProtocol : WebSocketProtocol
    {
        internal static readonly Logger EVENTSUB = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-EventSub.log") };

        private readonly TreatedEventBuffer m_TreatedEventBuffer;
        internal EventHandler? OnWelcome;
        internal EventHandler? OnReconnect;
        internal EventHandler? OnUnwantedDisconnect;
        private readonly ITwitchHandler? m_TwitchHandler;
        private readonly Token m_Token;
        private readonly Dictionary<string, AEventSubSubscription> m_Subscriptions = [];
        private readonly string m_ChannelID;

        public EventSubProtocol(TreatedEventBuffer treatedEventBuffer, TwitchAPI api, string channelID, Token token, ITwitchHandler? twitchHandler, SubscriptionType[] subscriptionTypes) : base(new Dictionary<string, string>() { { "Authorization", string.Format("Bearer {0}", token!.AccessToken) }})
        {
            m_TreatedEventBuffer = treatedEventBuffer;
            m_TwitchHandler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;

            foreach (SubscriptionType subscriptionType in subscriptionTypes)
            {
                switch(subscriptionType)
                {
                    case SubscriptionType.ChannelFollow: AddEventSubSubscription(new ChannelFollow(twitchHandler)); break;
                    case SubscriptionType.ChannelSubscribe: AddEventSubSubscription(new ChannelSubscribe(twitchHandler)); break;
                    case SubscriptionType.ChannelSubscriptionGift: AddEventSubSubscription(new ChannelSubscriptionGift(twitchHandler)); break;
                    case SubscriptionType.ChannelRaid: AddEventSubSubscription(new ChannelRaid(twitchHandler)); break;
                    case SubscriptionType.ChannelChannelPointsCustomRewardRedemptionAdd: AddEventSubSubscription(new ChannelChannelPointsCustomRewardRedemptionAdd(twitchHandler)); break;
                    case SubscriptionType.StreamOnline: AddEventSubSubscription(new StreamOnline(twitchHandler)); break;
                    case SubscriptionType.StreamOffline: AddEventSubSubscription(new StreamOffline(twitchHandler)); break;
                    case SubscriptionType.ChannelShoutoutCreate: AddEventSubSubscription(new ChannelShoutoutCreate(twitchHandler)); break;
                    case SubscriptionType.ChannelShoutoutReceive: AddEventSubSubscription(new ChannelShoutoutReceive(twitchHandler)); break;
                    case SubscriptionType.ChannelChatClear: AddEventSubSubscription(new ChannelChatClear(twitchHandler)); break;
                    case SubscriptionType.ChannelChatClearUserMessages: AddEventSubSubscription(new ChannelChatClearUserMessages(twitchHandler)); break;
                    case SubscriptionType.ChannelChatMessage: AddEventSubSubscription(new ChannelChatMessage(api, twitchHandler)); break;
                    case SubscriptionType.ChannelChatMessageDelete: AddEventSubSubscription(new ChannelChatMessageDelete(twitchHandler)); break;
                    case SubscriptionType.ChannelChatNotification: AddEventSubSubscription(new ChannelChatNotification(api, twitchHandler)); break;
                }
            }
        }

        private void AddEventSubSubscription(AEventSubSubscription subscription) => m_Subscriptions[subscription.Name] = subscription;

        protected override void OnWSOpen(Response message)
        {
            SetReadOnly(true);
            EVENTSUB.Log(string.Format("WS Open : {0}", message.ToString()));
        }

        protected override void OnWSClose(int status, string message)
        {
            if (status == 4002)
            {
                OnUnwantedDisconnect?.Invoke(this, EventArgs.Empty);
                EVENTSUB.Log("WS Close (4002) : Ping pong failuere");
            }
            else
                EVENTSUB.Log(string.Format("WS Close ({0}) : {1}", status, message));
        }

        protected override void OnWSMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            JObject eventMessage = JObject.Parse(message);
            if (eventMessage.TryGet("metadata", out JObject? metadataObj) && eventMessage.TryGet("payload", out JObject? payload))
            {
                Metadata metadata = new(metadataObj!);
                if (m_TreatedEventBuffer.PushEventID(metadata.ID))
                {
                    switch (metadata.Type)
                    {
                        case "session_welcome":
                        {
                            if (m_Token != null && payload!.TryGet("session", out JObject? sessionObj) && sessionObj!.TryGet("id", out string? sessionID))
                            {
                                foreach (var pair in m_Subscriptions)
                                    pair.Value.RegisterSubscription(m_Token, sessionID!, m_ChannelID);
                                OnWelcome?.Invoke(this, EventArgs.Empty);
                            }
                            break;
                        }
                        case "session_keepalive":
                            break;
                        case "notification":
                        {
                            if (payload!.TryGet("subscription", out JObject? subscriptionObj) && payload!.TryGet("event", out JObject? eventObj))
                            {
                                Subscription subscription = new(subscriptionObj!);
                                EventData eventData = new(eventObj!);
                                if (m_Subscriptions.TryGetValue(subscription.Type, out AEventSubSubscription? eventSubSubscription))
                                    eventSubSubscription.HandleEvent(subscription, eventData);
                                else
                                    m_TwitchHandler?.UnhandledEventSub(message.Trim());
                            }
                            break;
                        }
                        case "session_reconnect":
                        {
                            OnReconnect?.Invoke(this, EventArgs.Empty);
                            break;
                        }
                        case "revocation":
                            break;
                        default:
                        {
                            m_TwitchHandler?.UnhandledEventSub(message.Trim());
                            break;
                        }
                    }
                }
            }
        }

        protected override void OnClientDisconnected()
        {
            EVENTSUB.Log("<= Disconnected");
        }

        protected override void OnDiscardException(Exception exception)
        {
            EVENTSUB.Log(exception.ToString());
        }
    }
}
