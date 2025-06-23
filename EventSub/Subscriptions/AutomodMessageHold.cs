using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class AutomodMessageHold(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "automod.message.hold", 2)
    {
        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "moderator_user_id", channelID }
        };

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (ExtractUserInfo(data, "user_id", out TwitchUser? user, out string? color))
            {
                if (data.TryGet("message_id", out string? messageID) &&
                    data.TryGet("message", out DataObject? message))
                {
                    //TODO Handle automod section
                    /*
                     "automod": {
                        "category": "aggressive",
                        "level": 1,
                        "boundaries": [
                            {
                                "start_pos": 0,
                                "end_pos": 10
                            },
                            {
                                "start_pos": 20,
                                "end_pos": 30
                            }
                        ]
                    }*/
                    Text chatMessage = SubscriptionHelper.ConvertFragments(API, message!.GetList<DataObject>("fragments"));
                    Handler?.OnMessageHeld(user!, messageID!, chatMessage);
                }
            }
        }
    }
}
