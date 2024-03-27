using CorpseLib.Json;
using CorpseLib.StructuredText;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class AutomodMessageHold(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "automod.message.hold", 1)
    {
        protected override JsonObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (ExtractUserInfo(data, "user_id", out TwitchUser? user, out string? color))
            {
                if (data.TryGet("message_id", out string? messageID) &&
                    data.TryGet("message", out JsonObject? message))
                {
                    Text chatMessage = ConvertFragments(message!.GetList<JsonObject>("fragments"));
                    Handler?.OnMessageHeld(user!, messageID!, chatMessage);
                }
            }
        }
    }
}
