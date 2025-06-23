using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsCustomRewardRedemptionAdd(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.channel_points_custom_reward_redemption.add", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchUser? viewer = data.GetUser();
            if (viewer != null && data.TryGet("id", out string? redemptionID) && data.TryGet("reward", out DataObject? rewardInfo))
            {
                data.TryGet("user_input", out string? input);
                if (rewardInfo!.TryGet("id", out string? rewardID))
                    Handler?.OnRewardClaimed(viewer, new(redemptionID!, rewardID!), [new TextSection(input ?? string.Empty)]);
            }
        }
    }
}
