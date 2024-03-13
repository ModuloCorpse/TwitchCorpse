using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class StreamOnline(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "stream.online", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new() { { "broadcaster_user_id", channelID } };
        protected override void Treat(Subscription subscription, EventData data) => Handler?.OnStreamStart();
    }
}
