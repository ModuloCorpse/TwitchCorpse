using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatNotification(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "channel.chat.notification", 1)
    {
        private static readonly Dictionary<string, string> ms_Colors = new()
        {
            { "PRIMARY", "#ff0000"}, //Red
            { "GREEN", "#00ff00" }, //Green
            { "ORANGE", "#daa520" }, //Yellow-orange
            { "BLUE", "#1e90ff" }, //Blue
            { "PURPLE", "#8a2be2" } //Purple
        };

        private static int GetSubTier(DataObject subNotification)
        {
            if (subNotification.TryGet("is_prime", out bool? prime) && (bool)prime!)
                return 4;
            else if (subNotification.TryGet("sub_tier", out string? subTier))
                return subTier switch { "1000" => 1, "2000" => 2, "3000" => 3, _ => -1 };
            return -1;
        }

        protected override void Treat(Subscription subscription, EventData data)
        {
            if (ExtractUserInfo(data, out TwitchUser? user, out string? color))
            {
                if (data.TryGet("message_id", out string? messageID) &&
                    data.TryGet("message", out DataObject? message) &&
                    data.TryGet("notice_type", out string? noticeType))
                {
                    Text chatMessage = ConvertFragments(message!.GetList<DataObject>("fragments"));
                    switch (noticeType!)
                    {
                        case "sub":
                        {
                            if (data.TryGet("sub", out DataObject? sub) && sub != null)
                            {
                                int followTier = GetSubTier(sub);
                                if (followTier == -1)
                                    return;
                                if (!sub.TryGet("duration_months", out int? cumulativeMonth) || cumulativeMonth == null)
                                    cumulativeMonth = 1;
                                Handler?.OnChatMessage(user!, true, messageID!, string.Empty, color!, chatMessage);
                                Handler?.OnSharedSub(user!, followTier, (int)cumulativeMonth!, -1, chatMessage);
                            }
                            break;
                        }
                        case "resub":
                        {
                            if (data.TryGet("resub", out DataObject? resub) && resub != null)
                            {
                                int followTier = GetSubTier(resub);
                                if (followTier == -1)
                                    return;
                                if (!resub.TryGet("duration_months", out int? cumulativeMonth) || cumulativeMonth == null)
                                    cumulativeMonth = 1;
                                if (!resub.TryGet("streak_months", out int? monthStreak) || monthStreak == null)
                                    monthStreak = -1;
                                if (resub.TryGet("is_gift", out bool? isGift) && (bool)isGift!)
                                {
                                    if (resub.TryGet("gifter_user_id", out string? gifterID) && gifterID != null)
                                    {
                                        TwitchUser? gifter = null;
                                        if (gifterID != null)
                                            gifter = API.GetUserInfoFromID(gifterID!);
                                        Handler?.OnChatMessage(user!, true, messageID!, string.Empty, color!, chatMessage);
                                        Handler?.OnSharedGiftSub(gifter, user!, followTier, (int)cumulativeMonth!, (int)monthStreak!, chatMessage);
                                    }
                                }
                                else
                                {
                                    Handler?.OnChatMessage(user!, true, messageID!, string.Empty, color!, chatMessage);
                                    Handler?.OnSharedSub(user!, followTier, (int)cumulativeMonth!, (int)monthStreak!, chatMessage);
                                }
                            }
                            break;
                        }
                        case "sub_gift":
                        {
                            if (data.TryGet("sub_gift", out DataObject? subGift) && subGift != null)
                            {
                                int followTier = GetSubTier(subGift);
                                if (followTier == -1)
                                    return;
                                if (subGift.TryGet("recipient_user_id", out string? userID))
                                {
                                    TwitchUser? recipient = API.GetUserInfoFromID(userID!);
                                    if (user == null)
                                        return;
                                    if (!subGift.TryGet("duration_months", out int? monthGifted) || monthGifted == null)
                                        monthGifted = 1;
                                    Handler?.OnChatMessage(user!, true, messageID!, string.Empty, color!, chatMessage);
                                    Handler?.OnSharedGiftSub(user!, recipient!, followTier, (int)monthGifted!, -1, chatMessage);
                                }
                            }
                            break;
                        }
                        case "announcement":
                        {
                            if (data.TryGet("announcement", out DataObject? announcement) && announcement != null)
                            {
                                if (announcement.TryGet("color", out string? announcementBorderColor) && announcementBorderColor != null &&
                                    ms_Colors.TryGetValue(announcementBorderColor, out string? announcementColor))
                                    Handler?.OnChatMessage(user!, false, messageID!, announcementColor!, color!, chatMessage);
                            }
                            break;
                        }
                        case "community_sub_gift":
                        {
                            //TODO
                            break;
                        }
                    }
                }
            }
        }
    }
}
