using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelPointsAutomaticRewardRedemptionAdd(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.channel_points_automatic_reward_redemption.add", 2)
    {
        private readonly TwitchAPI m_API = api;

        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            TwitchUser? viewer = data.GetUser();
            if (viewer != null && data.TryGet("id", out string? redemptionID) && data.TryGet("reward", out DataObject? rewardInfo))
            {
                Text input = [];
                if (data.TryGet("message", out DataObject? message))
                    input = SubscriptionHelper.ConvertFragments(m_API, message!.GetList<DataObject>("fragments"));
                if (rewardInfo!.TryGet("id", out string? rewardID))
                    Handler?.OnRewardClaimed(viewer, new(redemptionID!, rewardID!), input);
            }
        }
    }
}
