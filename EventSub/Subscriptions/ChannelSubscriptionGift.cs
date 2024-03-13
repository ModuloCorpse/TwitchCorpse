using CorpseLib.Json;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelSubscriptionGift(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.subscription.gift", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            int followTier;
            if (data.TryGet("is_anonymous", out bool? isAnonymous))
            {
                TwitchUser? follower = data.GetUser();
                if (((bool)isAnonymous! || follower != null) && data.TryGet("tier", out string? tier) && data.TryGet("total", out int? nbGift))
                {
                    switch (tier!)
                    {
                        case "1000": followTier = 1; break;
                        case "2000": followTier = 2; break;
                        case "3000": followTier = 3; break;
                        default: return;
                    }
                    Handler?.OnGiftSub(follower, followTier, (int)nbGift!);
                }
            }
        }
    }
}
