using CorpseLib.Json;
using CorpseLib.Web.OAuth;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelRaid(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.raid", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new();

        internal override void RegisterSubscription(Token token, string sessionID, string channelID)
        {
            RegisterEventSubSubscription(token, Name, sessionID, Version, new() { { "to_broadcaster_user_id", channelID } });
            RegisterEventSubSubscription(token, Name, sessionID, Version, new() { { "from_broadcaster_user_id", channelID } });
        }

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("viewers", out int? viewers))
            {
                if (!(subscription.HaveCondition("from_broadcaster_user_id") && !string.IsNullOrEmpty(subscription.GetCondition("from_broadcaster_user_id"))))
                {
                    TwitchUser? from = data.GetUser("from_broadcaster_");
                    if (from != null)
                        Handler?.OnRaided(from, (int)viewers!);
                }
                else
                {
                    TwitchUser? to = data.GetUser("to_broadcaster_");
                    if (to != null)
                        Handler?.OnRaiding(to, (int)viewers!);
                }
            }
        }
    }
}
