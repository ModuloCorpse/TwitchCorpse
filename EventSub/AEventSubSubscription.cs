using CorpseLib.Json;
using CorpseLib.Network;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub
{
    internal abstract class AEventSubSubscription(ITwitchHandler? twitchHandler, string subscriptionName, int subscriptionVersion)
    {
        private readonly ITwitchHandler? m_TwitchHandler = twitchHandler;
        private readonly string m_SubscriptionName = subscriptionName;
        private readonly int m_SubscriptionVersion = subscriptionVersion;

        protected ITwitchHandler? Handler => m_TwitchHandler;
        internal string Name => m_SubscriptionName;
        internal int Version => m_SubscriptionVersion;

        internal static void RegisterEventSubSubscription(Token token, string subscriptionName, string sessionID, int subscriptionVersion, JsonObject condition)
        {
            JsonObject message = new()
            {
                { "type", subscriptionName },
                { "version", subscriptionVersion },
                { "condition", condition },
                { "transport", new JsonObject()
                    {
                        { "method", "websocket" },
                        { "session_id", sessionID }
                    }
                }
            };
            URLRequest request = new(URI.Parse("https://api.twitch.tv/helix/eventsub/subscriptions"), Request.MethodType.POST, message.ToNetworkString());
            request.AddContentType(MIME.APPLICATION.JSON);
            request.AddRefreshToken(token);
            Log(string.Format("Sending: {0}", request.Request.ToString()));
            Response response = request.Send();
            if (response.StatusCode == 202)
                Log(string.Format("<= Listening to {0}", subscriptionName));
            else
                Log(string.Format("<= Error when listening to {0}: {1}", subscriptionName, response.Body));
        }

        internal virtual void RegisterSubscription(Token token, string sessionID, string channelID)
        {
            RegisterEventSubSubscription(token, m_SubscriptionName, sessionID, m_SubscriptionVersion, GenerateSubscriptionCondition(channelID));
        }


        internal void HandleEvent(Subscription subscription, EventData data) => Treat(subscription, data);

        protected static void Log(string log) => EventSubProtocol.EVENTSUB.Log(log);

        protected abstract JsonObject GenerateSubscriptionCondition(string channelID);
        protected abstract void Treat(Subscription subscription, EventData data);
    }
}
