using CorpseLib;
using CorpseLib.DataNotation;
using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Network.OAuth;
using CorpseLib.Network.WebSocket;
using System.Diagnostics;
using System.Timers;
using TwitchCorpse.EventSub.Core;
using TwitchCorpse.EventSub.Subscriptions;
using static TwitchCorpse.TwitchEventSub;

namespace TwitchCorpse.EventSub
{
    public class EventSubProtocol : AWebSocketProtocol
    {
        public static readonly Logger EVENTSUB = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}");

        private readonly Stopwatch m_KeepAliveStopwatch = new();
        private readonly RecurringAction m_KeepAliveTimer = new(TimeSpan.FromSeconds(1));
        private readonly TreatedEventBuffer m_TreatedEventBuffer;
        internal AsyncEventHandler? OnWelcome;
        internal AsyncEventHandler? OnReconnect;
        internal AsyncEventHandler? OnUnwantedDisconnect;
        private readonly ITwitchHandler m_TwitchHandler;
        private readonly Token m_Token;
        private readonly Dictionary<string, AEventSubSubscription> m_Subscriptions = [];
        private readonly string m_ChannelID;
        private TimeSpan m_KeepAliveTimeoutDuration = TimeSpan.MaxValue;

        public EventSubProtocol(TreatedEventBuffer treatedEventBuffer, TwitchAPI api, string channelID, Token token, ITwitchHandler twitchHandler, SubscriptionType[] subscriptionTypes) : base()
        {
            SetIsReadOnly(true);
            m_TreatedEventBuffer = treatedEventBuffer;
            m_TwitchHandler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
            m_KeepAliveTimer.OnUpdate += UpdateKeepalive;

            foreach (SubscriptionType subscriptionType in subscriptionTypes)
            {
                switch(subscriptionType)
                {
                    case SubscriptionType.ChannelFollow: AddEventSubSubscription(new ChannelFollow(twitchHandler)); break;
                    case SubscriptionType.ChannelSubscribe: AddEventSubSubscription(new ChannelSubscribe(twitchHandler)); break;
                    case SubscriptionType.ChannelSubscriptionGift: AddEventSubSubscription(new ChannelSubscriptionGift(twitchHandler)); break;
                    case SubscriptionType.ChannelRaid: AddEventSubSubscription(new ChannelRaid(twitchHandler)); break;
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
                    case SubscriptionType.ChannelPointsCustomRewardRedemptionAdd: AddEventSubSubscription(new ChannelPointsCustomRewardRedemptionAdd(twitchHandler)); break;
                    case SubscriptionType.ChannelPointsCustomRewardRedemptionUpdate: AddEventSubSubscription(new ChannelPointsCustomRewardRedemptionUpdate(twitchHandler)); break;
                    case SubscriptionType.ChannelPointsCustomRewardAdd: AddEventSubSubscription(new ChannelPointsCustomRewardAdd(twitchHandler)); break;
                    case SubscriptionType.ChannelPointsCustomRewardRemove: AddEventSubSubscription(new ChannelPointsCustomRewardRemove(twitchHandler)); break;
                    case SubscriptionType.ChannelPointsCustomRewardUpdate: AddEventSubSubscription(new ChannelPointsCustomRewardUpdate(twitchHandler)); break;
                    case SubscriptionType.ChannelPointsAutomaticRewardRedemptionAdd: AddEventSubSubscription(new ChannelPointsAutomaticRewardRedemptionAdd(api, twitchHandler)); break;
                }
            }
        }

        private async Task StartKeepAliveTimer(TimeSpan keepAliveTimeoutDuration)
        {
            m_KeepAliveTimeoutDuration = keepAliveTimeoutDuration;
            m_KeepAliveStopwatch.Start();
            await m_KeepAliveTimer.Start();
        }

        private void ResetKeepAliveTimer()
        {
            m_KeepAliveStopwatch.Restart();
        }

        private async Task UpdateKeepalive()
        {
            if (m_KeepAliveStopwatch.Elapsed >= m_KeepAliveTimeoutDuration)
            {
                m_KeepAliveStopwatch.Stop();
                await m_KeepAliveTimer.Stop();
                await Reconnect();
            }
        }

        private void AddEventSubSubscription(AEventSubSubscription subscription) => m_Subscriptions[subscription.Name] = subscription;

        public override async Task OnOpen() { }
        public override async Task OnClose(int status, string message)
        {
            if (status == 4002)
            {
                await CorpseLib.Helper.CallAsyncEventHandler(OnUnwantedDisconnect);
                EVENTSUB.Log("WS Close (4002) : Ping pong failure : ${0}", message);
            }
            else
                EVENTSUB.Log("WS Close (${0}) : ${1}", status, message);
            m_KeepAliveStopwatch.Stop();
            await m_KeepAliveTimer.Stop();
            EVENTSUB.Log("<= Disconnected");
        }

        public override async Task OnMessageReceived(string message)
        {
            EVENTSUB.Log($"=> {message}");
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
                                await StartKeepAliveTimer(TimeSpan.FromSeconds(keepaliveTimeoutSeconds));
                            if (m_Token != null && payload!.TryGet("session", out DataObject? sessionObj) && sessionObj!.TryGet("id", out string? sessionID))
                            {
                                foreach (var pair in m_Subscriptions)
                                    pair.Value.RegisterSubscription(m_Token, sessionID!, m_ChannelID);
                                await CorpseLib.Helper.CallAsyncEventHandler(OnWelcome);
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
                                    await eventSubSubscription.HandleEvent(subscription, eventData);
                                else
                                    await m_TwitchHandler.UnhandledEventSub(message.Trim());
                            }
                            break;
                        }
                        case "session_reconnect":
                        {
                            await CorpseLib.Helper.CallAsyncEventHandler(OnReconnect);
                            break;
                        }
                        case "revocation":
                            break;
                        default:
                        {
                            await m_TwitchHandler.UnhandledEventSub(message.Trim());
                            break;
                        }
                    }
                }
            }
        }

        public override async Task OnError(Exception ex)
        {
            EVENTSUB.Log(ex.ToString());
        }
    }
}
