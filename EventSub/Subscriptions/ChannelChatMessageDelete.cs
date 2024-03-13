using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatMessageDelete(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.chat.message_delete", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("message_id", out string? messageID))
                Handler?.OnChatMessageRemoved(messageID!);
        }
    }
}
