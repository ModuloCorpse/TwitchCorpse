using CorpseLib.Json;
using CorpseLib.StructuredText;
using CorpseLib.Web.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal class ChannelChatNotification(TwitchAPI api, ITwitchHandler? twitchHandler) : AEventSubChatMessageSubscription(api, twitchHandler, "channel.chat.notification", 1)
    {
        private static int GetSubTier(JObject subNotification)
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
                    data.TryGet("message", out JObject? message))
                {
                    Text chatMessage = ConvertFragments(message!.GetList<JObject>("fragments"));
                    Handler?.OnChatMessage(user!, true, messageID!, color!, chatMessage);

                    if (data.TryGet("cheer", out JObject? cheer) && cheer != null && cheer.TryGet("bits", out int? bits))
                        Handler?.OnBits(user!, (int)bits!, chatMessage);

                    if (data.TryGet("notice_type", out string? noticeType))
                    {
                        switch (noticeType!)
                        {
                            case "sub":
                            {
                                if (data.TryGet("sub", out JObject? sub) && sub != null)
                                {
                                    int followTier = GetSubTier(sub);
                                    if (followTier == -1)
                                        return;
                                    if (!sub.TryGet("duration_months", out int? cumulativeMonth))
                                        cumulativeMonth = 1;
                                    Handler?.OnSharedSub(user!, followTier, (int)cumulativeMonth!, -1, chatMessage);
                                }
                                break;
                            }
                            case "resub":
                            {
                                if (data.TryGet("resub", out JObject? resub) && resub != null)
                                {
                                    int followTier = GetSubTier(resub);
                                    if (followTier == -1)
                                        return;
                                    if (!resub.TryGet("duration_months", out int? cumulativeMonth))
                                        cumulativeMonth = 1;
                                    if (!resub.TryGet("streak_months", out int? monthStreak))
                                        monthStreak = -1;
                                    if (resub.TryGet("is_gift", out bool? isGift) && (bool)isGift!)
                                    {
                                        if (resub.TryGet("gifter_user_id", out string? gifterID) && gifterID != null)
                                        {
                                            TwitchUser? gifter = null;
                                            if (gifterID != null)
                                                gifter = API.GetUserInfoFromID(gifterID!);
                                            Handler?.OnSharedGiftSub(gifter, user!, followTier, (int)cumulativeMonth!, (int)monthStreak!, chatMessage);
                                        }
                                    }
                                    else
                                        Handler?.OnSharedSub(user!, followTier, (int)cumulativeMonth!, (int)monthStreak!, chatMessage);
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
