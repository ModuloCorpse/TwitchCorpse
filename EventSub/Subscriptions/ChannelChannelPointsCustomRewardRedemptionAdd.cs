using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChannelPointsCustomRewardRedemptionAdd(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.channel_points_custom_reward_redemption.add", 1)
    {
        protected override JObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchUser? viewer = data.GetUser();
            if (viewer != null && data.TryGet("reward", out JObject? rewardInfo))
            {
                data.TryGet("user_input", out string? input);
                if (rewardInfo!.TryGet("title", out string? title))
                    Handler?.OnReward(viewer, title!, input ?? string.Empty);
            }
        }
    }
}
