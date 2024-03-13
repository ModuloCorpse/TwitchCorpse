using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatClear(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.chat.clear", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data) => Handler?.OnChatClear();
    }
}
