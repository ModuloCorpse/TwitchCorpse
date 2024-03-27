using CorpseLib.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class AutomodMessageUpdate(ITwitchHandler? twitchHandler) : AEventSubSubscription(twitchHandler, "automod.message.update", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (data.TryGet("message_id", out string? messageID))
                Handler?.OnHeldMessageTreated(messageID!);
        }
    }
}
