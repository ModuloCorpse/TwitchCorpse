using CorpseLib.DataNotation;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelFollow(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "channel.follow", 2)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override async Task Treat(Subscription subscription, EventData data)
        {
            TwitchUser? follower = data.GetUser();
            if (follower != null)
                await Handler.OnFollow(follower);
        }
    }
}
