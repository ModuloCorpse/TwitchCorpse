using CorpseLib;
using CorpseLib.Logging;
using CorpseLib.Network.OAuth;
using CorpseLib.Network.WebSocket;
using System.Reflection.Metadata;
using TwitchCorpse.EventSub;
using static TwitchCorpse.EventSub.EventSubProtocol;

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
            ChannelPointsCustomRewardRedemptionUpdate,
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
        public AsyncEventHandler? OnWelcome;
        private readonly TwitchAPI m_API;
        private readonly ITwitchHandler m_Handler;
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

        internal TwitchEventSub(TwitchAPI api, string channelID, Token token, ITwitchHandler twitchHandler)
        {
            m_API = api;
            m_Handler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
        }

        internal TwitchEventSub(TwitchAPI api, string channelID, Token token, SubscriptionType[] subscriptionTypes, ITwitchHandler twitchHandler)
        {
            m_API = api;
            m_Handler = twitchHandler;
            m_Token = token;
            m_ChannelID = channelID;
            m_SubscriptionTypes = subscriptionTypes;
        }

        internal async Task InitProtocol(bool shouldHandleDisconnet)
        {
            m_Protocol = await NewProtocol(true);
            if (shouldHandleDisconnet)
                m_Protocol.OnUnwantedDisconnect += HandleMainClientDisconnect;
        }

        private async Task<EventSubProtocol> NewProtocol(bool firstConnection)
        {
            EventSubProtocol client = new(m_TreatedEventBuffer, m_API, m_ChannelID, m_Token, m_Handler, m_SubscriptionTypes);
            Dictionary<string, string> headers = new()
            {
                {"Authorization", $"Bearer {m_Token.AccessToken}" }
            };
            if (firstConnection)
                client.OnWelcome += async () => await CorpseLib.Helper.CallAsyncEventHandler(OnWelcome);
            WebSocketClient? ws = await WebSocketClient.Connect(URI.Parse("wss://eventsub.wss.twitch.tv/ws"), client, headers);
            client.OnReconnect += HandleClientReconnect;
            return client;
        }

        private async Task HandleMainClientDisconnect()
        {
            m_Protocol = await NewProtocol(false);
            m_Protocol.OnUnwantedDisconnect += HandleMainClientDisconnect;
        }

        private async Task HandleClientReconnect()
        {
            if (m_ReconnectProtocol != null)
                await m_ReconnectProtocol.Disconnect();
            m_ReconnectProtocol = await NewProtocol(false);
            m_ReconnectProtocol.OnWelcome += HandleReconnectWelcome;
        }

        private async Task HandleReconnectWelcome()
        {
            if (m_Protocol != null)
                await m_Protocol.Disconnect();
            m_Protocol = m_ReconnectProtocol;
            m_Protocol!.OnUnwantedDisconnect += HandleMainClientDisconnect;
            m_ReconnectProtocol!.OnWelcome -= HandleReconnectWelcome;
        }

        public async Task Disconnect()
        {
            if (m_Protocol != null)
                await m_Protocol.Disconnect();
            if (m_ReconnectProtocol != null)
                await m_ReconnectProtocol.Disconnect();
        }
    }
}
