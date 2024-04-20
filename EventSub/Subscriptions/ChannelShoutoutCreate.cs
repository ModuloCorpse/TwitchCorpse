using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelShoutoutCreate(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.shoutout.create", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchUser? moderator = data.GetUser("moderator_");
            TwitchUser? to = data.GetUser("to_broadcaster_");
            if (moderator != null && to != null)
                Handler?.OnShoutout(moderator, to);
        }
    }
}
