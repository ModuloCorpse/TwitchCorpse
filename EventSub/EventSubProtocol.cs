using CorpseLib.DataNotation;
using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using System.Diagnostics;
using System.Timers;
using TwitchCorpse.EventSub.Core;
using TwitchCorpse.EventSub.Subscriptions;
using static TwitchCorpse.TwitchEventSub;

namespace TwitchCorpse.EventSub
{
    internal class EventSubProtocol : WebSocketProtocol
    {
        internal static readonly Logger EVENTSUB = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-EventSub.log") };

        private readonly Stopwatch m_KeepAliveStopwatch = new();
        private readonly System.Timers.Timer m_KeepAliveTimer = new(TimeSpan.FromSeconds(1));
        private readonly TreatedEventBuffer m_TreatedEventBuffer;
        internal EventHandler? OnWelcome;
        internal EventHandler? OnReconnect;
        internal EventHandler? OnUnwantedDisconnect;
        private readonly ITwitchHandler? m_TwitchHandler;
        private readonly Token m_Token;
        private readonly Dictionary<string, AEventSubSubscription> m_Subscriptions = [];
        private readonly string m_ChannelID;
        private TimeSpan m_KeepAliveTimeoutDuration = TimeSpan.MaxValue;

        public EventSubProtocol(TreatedEventBuffer treatedEventBuffer, TwitchAPI api, string channelID, Token token, ITwitchHandler? twitchHandler, SubscriptionType[] subscriptionTypes) : base(new Dictionary<string, string>() { { "Authorization", string.Format("Bearer {0}", token!.AccessToken) }})
        {
            m_TreatedEventBuffer = treatedEventBuffer;
            m_TwitchHandler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
            m_KeepAliveTimer.Elapsed += UpdateKeepalive;
            m_KeepAliveTimer.AutoReset = true;

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
                    case SubscriptionType.AutomodMessageHeld: AddEventSubSubscription(new AutomodMessageHold(api, twitchHandler)); break;
                    case SubscriptionType.AutomodMessageUpdate: AddEventSubSubscription(new AutomodMessageUpdate(twitchHandler)); break;
                    case SubscriptionType.SharedChatBegin: AddEventSubSubscription(new SharedChatBegin(twitchHandler)); break;
                    case SubscriptionType.SharedChatEnd: AddEventSubSubscription(new SharedChatEnd(twitchHandler)); break;
                }
            }
        }

        private void StartKeepAliveTimer(TimeSpan keepAliveTimeoutDuration)
        {
            m_KeepAliveTimeoutDuration = keepAliveTimeoutDuration;
            m_KeepAliveStopwatch.Start();
            m_KeepAliveTimer.Start();
        }

        private void ResetKeepAliveTimer()
        {
            m_KeepAliveStopwatch.Restart();
        }

        private void UpdateKeepalive(object? source, ElapsedEventArgs e)
        {
            if (m_KeepAliveStopwatch.Elapsed >= m_KeepAliveTimeoutDuration)
            {
                m_KeepAliveStopwatch.Stop();
                m_KeepAliveTimer.Stop();
                Reconnect();
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
            DataObject eventMessage = JsonParser.Parse(message);
            if (eventMessage.TryGet("metadata", out DataObject? metadataObj) && eventMessage.TryGet("payload", out DataObject? payload))
            {
                Metadata metadata = new(metadataObj!);
                ResetKeepAliveTimer();
                if (m_TreatedEventBuffer.PushEventID(metadata.ID))
                {
                    switch (metadata.Type)
                    {
                        case "session_welcome":
                        {
                            if (payload!.TryGet("keepalive_timeout_seconds", out int keepaliveTimeoutSeconds))
                                StartKeepAliveTimer(TimeSpan.FromSeconds(keepaliveTimeoutSeconds));
                            if (m_Token != null && payload!.TryGet("session", out DataObject? sessionObj) && sessionObj!.TryGet("id", out string? sessionID))
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
                            if (payload!.TryGet("subscription", out DataObject? subscriptionObj) && payload!.TryGet("event", out DataObject? eventObj))
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
            m_KeepAliveStopwatch.Stop();
            m_KeepAliveTimer.Stop();
            EVENTSUB.Log("<= Disconnected");
        }

        protected override void OnDiscardException(Exception exception)
        {
            EVENTSUB.Log(exception.ToString());
        }
    }
}
