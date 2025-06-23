using CorpseLib.DataNotation;
using CorpseLib.Web.OAuth;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelRaid(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "channel.raid", 1)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => [];

        protected override void OnRegisterSubscription(Token token, string sessionID, string channelID)
        {
            RegisterEventSubSubscription(token, Name, sessionID, Version, new() { { "to_broadcaster_user_id", channelID } });
            RegisterEventSubSubscription(token, Name, sessionID, Version, new() { { "from_broadcaster_user_id", channelID } });
        }

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("viewers", out int? viewers))
            {
                TwitchUser? from = data.GetUser("from_broadcaster_");
                TwitchUser? to = data.GetUser("to_broadcaster_");
                if (from != null && to != null)
                {
                    if (from.ID == ChannelID)
                        Handler?.OnRaiding(to, (int)viewers!);
                    else
                        Handler?.OnRaided(from, (int)viewers!);
                }
            }
        }
    }
}
