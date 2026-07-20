using CorpseLib.DataNotation;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsCustomRewardRedemptionUpdate(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "channel.channel_points_custom_reward_redemption.update", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override async Task Treat(Subscription subscription, EventData data)
        {
            TwitchUser? viewer = data.GetUser();
            if (viewer != null && data.TryGet("id", out string? redemptionID) && data.TryGet("status", out string? status))
            {
                bool isFulfilled = (status == "fulfilled");
                await Handler.OnRewardRedemptionStatusChanged(redemptionID!, isFulfilled);
            }
        }
    }
}
