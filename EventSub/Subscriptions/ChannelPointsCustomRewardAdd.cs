using TwitchCorpse.API;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsCustomRewardAdd(ITwitchHandler twitchHandler) : AEventSubChannelRewardSubscription(twitchHandler, "channel.channel_points_custom_reward.add", 1)
    {
        protected override async Task TreatReward(TwitchRewardInfo rewardInfo) => await Handler.OnRewardCreated(rewardInfo);
    }
}
