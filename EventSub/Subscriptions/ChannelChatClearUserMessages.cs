using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatClearUserMessages(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.chat.clear_user_messages", 1)
    {
        protected override JObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("target_user_id", out string? targetUserID))
                Handler?.OnChatUserRemoved(targetUserID!);
        }
    }
}
