using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatMessage(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "channel.chat.message", 1)
    {
        protected override void Treat(Subscription subscription, EventData data)
        {
            if (ExtractUserInfo(data, out TwitchUser? user, out string? color))
            {
                if (data.TryGet("message_id", out string? messageID) &&
                    data.TryGet("message_type", out string? messageType) &&
                    data.TryGet("message", out DataObject? message))
                {
                    Text chatMessage = ConvertFragments(message!.GetList<DataObject>("fragments"));
                    Handler?.OnChatMessage(user!, (messageType == "channel_points_highlighted"), messageID!, string.Empty, color!, chatMessage);

                    if (data.TryGet("cheer", out DataObject? cheer) && cheer != null && cheer.TryGet("bits", out int? bits))
                        Handler?.OnBits(user!, (int)bits!, chatMessage);
                }
            }
        }
    }
}
