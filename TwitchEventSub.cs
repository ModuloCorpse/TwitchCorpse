using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.Serialize;
using CorpseLib.Web.OAuth;
using TwitchCorpse.EventSub;

namespace TwitchCorpse
{
    public class TwitchEventSub
    {
        public enum SubscriptionType
        {
            AutomodMessageHeld,
            AutomodMessageUpdate,
            ChannelChatClear,
            ChannelChatClearUserMessages,
            ChannelChatMessage,
            ChannelChatMessageDelete,
            ChannelChatNotification,
            ChannelFollow,
            ChannelPointsAutomaticRewardRedemptionAdd,
            ChannelPointsCustomRewardAdd,
            ChannelPointsCustomRewardRedemptionAdd,
            ChannelPointsCustomRewardRemove,
            ChannelPointsCustomRewardUpdate,
            ChannelRaid,
            ChannelShoutoutCreate,
            ChannelShoutoutReceive,
            ChannelSubscribe,
            ChannelSubscriptionGift,
            SharedChatBegin,
            SharedChatEnd,
            StreamOffline,
            StreamOnline
        }

        public static Logger LOGGER => EventSubProtocol.EVENTSUB;

        public static void StartLogging() => EventSubProtocol.EVENTSUB.Start();
        public static void StopLogging() => EventSubProtocol.EVENTSUB.Stop();

        private readonly TreatedEventBuffer m_TreatedEventBuffer = new(10);
        public EventHandler? OnWelcome;
        private readonly TwitchAPI m_API;
        private readonly MonitorBatch m_Monitor = [];
        private readonly ITwitchHandler? m_Handler;
        private EventSubProtocol? m_Protocol;
        private EventSubProtocol? m_ReconnectProtocol = null;
        private readonly Token m_Token;
        private readonly SubscriptionType[] m_SubscriptionTypes = [
            SubscriptionType.AutomodMessageHeld,
            SubscriptionType.AutomodMessageUpdate,
            SubscriptionType.ChannelPointsCustomRewardRedemptionAdd,
            SubscriptionType.ChannelChatClear,
            SubscriptionType.ChannelChatClearUserMessages,
            SubscriptionType.ChannelChatMessage,
            SubscriptionType.ChannelChatMessageDelete,
            SubscriptionType.ChannelChatNotification,
            SubscriptionType.ChannelFollow,
            SubscriptionType.ChannelRaid,
            SubscriptionType.ChannelShoutoutCreate,
            SubscriptionType.ChannelShoutoutReceive,
            SubscriptionType.ChannelSubscribe,
            SubscriptionType.ChannelSubscriptionGift,
            SubscriptionType.SharedChatBegin,
            SubscriptionType.SharedChatEnd,
            SubscriptionType.StreamOffline,
            SubscriptionType.StreamOnline];
        private readonly string m_ChannelID;

        internal TwitchEventSub(TwitchAPI api, string channelID, Token token, ITwitchHandler? twitchHandler = null)
        {
            m_API = api;
            m_Handler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
            m_Protocol = NewProtocol(true);
        }

        internal TwitchEventSub(TwitchAPI api, string channelID, Token token, SubscriptionType[] subscriptionTypes, ITwitchHandler? twitchHandler = null)
        {
            m_API = api;
            m_Handler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
            m_SubscriptionTypes = subscriptionTypes;
            m_Protocol = NewProtocol(true);
            m_Protocol.OnUnwantedDisconnect += HandleMainClientDisconnect;
        }

        private EventSubProtocol NewProtocol(bool firstConnection)
        {
            EventSubProtocol protocol = new(m_TreatedEventBuffer, m_API, m_ChannelID, m_Token, m_Handler, m_SubscriptionTypes);
            TCPAsyncClient client = new(protocol, URI.Parse("wss://eventsub.wss.twitch.tv/ws"));
            if (!m_Monitor.IsEmpty())
                protocol.AddMonitor(m_Monitor);
            client.Start();
            if (firstConnection)
                protocol.OnWelcome += (object? sender, EventArgs e) => OnWelcome?.Invoke(sender, e);
            protocol.OnReconnect += HandleClientReconnect;
            return protocol;
        }

        private void HandleMainClientDisconnect(object? _, EventArgs e)
        {
            m_Protocol = NewProtocol(false);
            m_Protocol.OnUnwantedDisconnect += HandleMainClientDisconnect;
        }

        private void HandleClientReconnect(object? _, EventArgs e)
        {
            m_ReconnectProtocol?.Disconnect();
            m_ReconnectProtocol = NewProtocol(false);
            m_ReconnectProtocol.OnWelcome += HandleReconnectWelcome;
        }

        private void HandleReconnectWelcome(object? sender, EventArgs e)
        {
            m_Protocol?.Disconnect();
            m_Protocol = m_ReconnectProtocol;
            m_Protocol!.OnUnwantedDisconnect += HandleMainClientDisconnect;
            m_ReconnectProtocol!.OnWelcome -= HandleReconnectWelcome;
        }

        public void AddMonitor(IMonitor monitor)
        {
            m_Monitor.Add(monitor);
            m_Protocol?.AddMonitor(monitor);
            m_ReconnectProtocol?.AddMonitor(monitor);
        }

        public void Disconnect()
        {
            m_Protocol?.Disconnect();
            m_ReconnectProtocol?.Disconnect();
        }

        public URI GetURL() => m_Protocol!.GetURL();
        public int GetID() => m_Protocol!.GetID();
        public bool IsConnected() => m_Protocol!.IsConnected();
        public bool IsReconnecting() => m_Protocol!.IsReconnecting();
        public BytesWriter CreateBytesWriter() => m_Protocol!.CreateBytesWriter();
        public void TestRead(BytesWriter bytesWriter) => m_Protocol!.TestRead(bytesWriter);
    }
}
