using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class StreamOffline(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "stream.offline", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new() { { "broadcaster_user_id", channelID } };
        protected override async Task Treat(Subscription subscription, EventData data) => await Handler.OnStreamStop();
    }
}
