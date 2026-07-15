using CorpseLib;
using CorpseLib.Logging;
using CorpseLib.Network.OAuth;
using TwitchCorpse.EventSub;
using CorpseLib.Network.WebSocket;

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
            EventSubProtocol client = new(m_TreatedEventBuffer, m_API, m_ChannelID, m_Token, m_Handler, m_SubscriptionTypes);
            Dictionary<string, string> headers = new()
            {
                {"Authorization", $"Bearer {m_Token.AccessToken}" }
            };
            WebSocketClient? ws = WebSocketClient.Connect(URI.Parse("wss://eventsub.wss.twitch.tv/ws"), client, headers);
            if (firstConnection)
                client.OnWelcome += (object? sender, EventArgs e) => OnWelcome?.Invoke(sender, e);
            client.OnReconnect += HandleClientReconnect;
            return client;
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

        public void Disconnect()
        {
            m_Protocol?.Disconnect();
            m_ReconnectProtocol?.Disconnect();
        }
    }
}
