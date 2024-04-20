using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class AutomodMessageHold(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "automod.message.hold", 1)
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
                    Text chatMessage = ConvertFragments(message!.GetList<DataObject>("fragments"));
                    Handler?.OnMessageHeld(user!, messageID!, chatMessage);
                }
            }
        }
    }
}
