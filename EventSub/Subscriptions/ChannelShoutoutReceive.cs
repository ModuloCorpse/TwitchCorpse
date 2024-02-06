using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelShoutoutReceive(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.shoutout.receive", 1)
    {
        protected override JObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchUser? from = data.GetUser("from_broadcaster_");
            if (from != null)
                Handler?.OnBeingShoutout(from);
        }
    }
}
