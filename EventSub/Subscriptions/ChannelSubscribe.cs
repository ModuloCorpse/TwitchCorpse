using CorpseLib.DataNotation;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelSubscribe(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.subscribe", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            int followTier;
            TwitchUser? follower = data.GetUser();
            if (follower != null && data.TryGet("tier", out string? tier))
            {
                bool isGift = data.GetOrDefault("is_gift", false);
                switch (tier!)
                {
                    case "1000": followTier = 1; break;
                    case "2000": followTier = 2; break;
                    case "3000": followTier = 3; break;
                    default: return;
                }
                Handler?.OnSub(follower, followTier, isGift);
            }
        }
    }
}
