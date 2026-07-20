using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class SharedChatBegin(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "channel.shared_chat.begin", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new() { { "broadcaster_user_id", channelID } };
        protected override async Task Treat(Subscription subscription, EventData data) => await Handler.OnSharedChatStart();
    }
}
