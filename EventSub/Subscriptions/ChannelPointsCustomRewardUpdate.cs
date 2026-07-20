using TwitchCorpse.API;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsCustomRewardUpdate(ITwitchHandler twitchHandler) : AEventSubChannelRewardSubscription(twitchHandler, "channel.channel_points_custom_reward.update", 1)
    {
        protected override async Task TreatReward(TwitchRewardInfo rewardInfo) => await Handler.OnRewardUpdated(rewardInfo);
    }
}
