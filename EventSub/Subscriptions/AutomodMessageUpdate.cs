using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class AutomodMessageUpdate(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "automod.message.update", 2)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("message_id", out string? messageID))
                Handler?.OnHeldMessageTreated(messageID!);
        }
    }
}
