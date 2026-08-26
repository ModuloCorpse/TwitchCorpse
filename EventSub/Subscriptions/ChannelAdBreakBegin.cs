using CorpseLib.DataNotation;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    public class ChannelAdBreakBegin(ITwitchHandler twitchHandler) : AEventSubSubscription(twitchHandler, "channel.ad_break.begin", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new() {{ "broadcaster_user_id", channelID }};
        protected override async Task Treat(Subscription subscription, EventData data)
        {
            int adDuration = data.GetOrDefault("duration_seconds", -1);
            bool isAutomatic = data.GetOrDefault("is_automatic", false);
            if (adDuration != -1)
                await Handler.OnAdBreakBegin(TimeSpan.FromSeconds(adDuration), isAutomatic);
        }
    }
}
