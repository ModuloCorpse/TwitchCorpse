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
                AEventSubSubscription? subscription = subscriptionType switch
                {
                    SubscriptionType.AutomodMessageHeld => new AutomodMessageHold(api, twitchHandler),
                    SubscriptionType.AutomodMessageUpdate => new AutomodMessageUpdate(twitchHandler),
                    SubscriptionType.ChannelAdBreakBegin => new ChannelAdBreakBegin(twitchHandler),
                    SubscriptionType.ChannelChatClear => new ChannelChatClear(twitchHandler),
                    SubscriptionType.ChannelChatClearUserMessages => new ChannelChatClearUserMessages(twitchHandler),
                    SubscriptionType.ChannelChatMessage => new ChannelChatMessage(api, twitchHandler),
                    SubscriptionType.ChannelChatMessageDelete => new ChannelChatMessageDelete(twitchHandler),
                    SubscriptionType.ChannelChatNotification => new ChannelChatNotification(api, twitchHandler),
                    SubscriptionType.ChannelFollow => new ChannelFollow(twitchHandler),
                    SubscriptionType.ChannelPointsAutomaticRewardRedemptionAdd => new ChannelPointsAutomaticRewardRedemptionAdd(api, twitchHandler),
                    SubscriptionType.ChannelPointsCustomRewardAdd => new ChannelPointsCustomRewardAdd(twitchHandler),
                    SubscriptionType.ChannelPointsCustomRewardRedemptionAdd => new ChannelPointsCustomRewardRedemptionAdd(twitchHandler),
                    SubscriptionType.ChannelPointsCustomRewardRedemptionUpdate => new ChannelPointsCustomRewardRedemptionUpdate(twitchHandler),
                    SubscriptionType.ChannelPointsCustomRewardRemove => new ChannelPointsCustomRewardRemove(twitchHandler),
                    SubscriptionType.ChannelPointsCustomRewardUpdate => new ChannelPointsCustomRewardUpdate(twitchHandler),
                    SubscriptionType.ChannelRaid => new ChannelRaid(twitchHandler),
                    SubscriptionType.ChannelShoutoutCreate => new ChannelShoutoutCreate(twitchHandler),
                    SubscriptionType.ChannelShoutoutReceive => new ChannelShoutoutReceive(twitchHandler),
                    SubscriptionType.ChannelSubscribe => new ChannelSubscribe(twitchHandler),
                    SubscriptionType.ChannelSubscriptionGift => new ChannelSubscriptionGift(twitchHandler),
                    SubscriptionType.SharedChatBegin => new SharedChatBegin(twitchHandler),
                    SubscriptionType.SharedChatEnd => new SharedChatEnd(twitchHandler),
                    SubscriptionType.StreamOffline => new StreamOffline(twitchHandler),
                    SubscriptionType.StreamOnline => new StreamOnline(twitchHandler),
                    _ => null
                };
                if (subscription != null)
                    AddEventSubSubscription(subscription);
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
