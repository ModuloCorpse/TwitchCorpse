using CorpseLib.DataNotation;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal abstract class AEventSubChannelRewardSubscription(ITwitchHandler? twitchHandler, string subscriptionName, int subscriptionVersion) : AEventSubSubscription(twitchHandler, subscriptionName, subscriptionVersion)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchRewardInfo? rewardInfo = data.Cast<TwitchRewardInfo>();
            if (rewardInfo != null)
                TreatReward(rewardInfo);
        }

        protected abstract void TreatReward(TwitchRewardInfo rewardInfo);
    }
}
