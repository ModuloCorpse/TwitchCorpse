using TwitchCorpse.API;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsCustomRewardRemove(ITwitchHandler twitchHandler) : AEventSubChannelRewardSubscription(twitchHandler, "channel.channel_points_custom_reward.remove", 1)
    {
        protected override async Task TreatReward(TwitchRewardInfo rewardInfo) => await Handler.OnRewardDeleted(rewardInfo.ID);
    }
}
