using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatClear(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "channel.chat.clear", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        protected override async Task Treat(Subscription subscription, EventData data) => await Handler.OnChatClear();
    }
}
