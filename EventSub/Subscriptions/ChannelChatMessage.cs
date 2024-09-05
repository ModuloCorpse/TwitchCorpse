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
                if (data.TryGet("message_id", out string? messageID) && messageID != null &&
                    data.TryGet("message_type", out string? messageType) && messageType != null &&
                    data.TryGet("message", out DataObject? message) && message != null)
                {
                    string? replyID = null;
                    if (data.TryGet("reply", out DataObject? reply) && reply != null)
                        reply.TryGet("parent_message_id", out replyID);

                    Text chatMessage = ConvertFragments(message.GetList<DataObject>("fragments"));
                    Handler?.OnChatMessage(user!, (messageType == "channel_points_highlighted"), messageID, replyID, string.Empty, color!, chatMessage);

                    if (data.TryGet("cheer", out DataObject? cheer) && cheer != null && cheer.TryGet("bits", out int? bits))
                        Handler?.OnBits(user!, (int)bits!, chatMessage);
                }
            }
        }
    }
}
