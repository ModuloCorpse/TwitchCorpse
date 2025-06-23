using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using TwitchCorpse.API;
using TwitchCorpse.EventSub.Core;

namespace TwitchCorpse.EventSub.Subscriptions
{
    internal abstract class AEventSubChatMessageSubscription(TwitchAPI api, ITwitchHandler? twitchHandler, string subscriptionName, int subscriptionVersion) : AEventSubSubscription(twitchHandler, subscriptionName, subscriptionVersion)
    {
        private static readonly List<string> ms_Colors =
        [
            "#ff0000",
            "#00ff00",
            "#0000ff",
            "#b22222",
            "#ff7f50",
            "#9acd32",
            "#ff4500",
            "#2e8b57",
            "#daa520",
            "#d2691e",
            "#5f9ea0",
            "#1e90ff",
            "#ff69b4",
            "#8a2be2",
            "#00ff7f"
        ];

        private readonly TwitchAPI m_API = api;

        protected TwitchAPI API => m_API;

        protected override DataObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        protected bool ExtractUserInfo(EventData data, string idKey, out TwitchUser? user, out string? color)
        {
            color = null;
            user = null;
            if (data.TryGet(idKey, out string? userID))
            {
                TwitchUser? foundUser = m_API.GetUserInfoFromID(userID!);
                if (foundUser == null)
                    return false;
                if (!data.TryGet("color", out color) || string.IsNullOrEmpty(color))
                {
                    int colorIdx = 0;
                    foreach (char c in foundUser.Name)
                        colorIdx += c;
                    colorIdx %= ms_Colors.Count;
                    color = ms_Colors[colorIdx];
                }

                List<DataObject> badges = data.GetList<DataObject>("badges");
                List<TwitchBadgeInfo> userBadges = [];
                foreach (DataObject badge in badges)
                {
                    if (badge.TryGet("set_id", out string? setID) && setID != null &&
                        badge.TryGet("id", out string? id) && id != null)
                    {
                        TwitchBadgeInfo? badgeInfo = m_API.GetBadge(setID, id);
                        if (badgeInfo != null)
                            userBadges.Add(badgeInfo);
                    }
                }

                user = foundUser.ChatUser(userBadges);
                return true;
            }
            return false;
        }

        protected bool ExtractBroadcasterInfo(EventData data, out TwitchUser? broadcaster)
        {
            if (data.TryGet("source_broadcaster_user_id", out string? sourceUserID))
            {
                if (string.IsNullOrEmpty(sourceUserID))
                    data.TryGet("broadcaster_user_id", out sourceUserID);
                if (string.IsNullOrEmpty(sourceUserID))
                {
                    broadcaster = null;
                    return false;
                }
                broadcaster = m_API.GetUserInfoFromID(sourceUserID);
                return (broadcaster != null);
            }
            broadcaster = null;
            return false;
        }

        protected bool ExtractUserInfo(EventData data, out TwitchUser? user, out string? color) => ExtractUserInfo(data, "chatter_user_id", out user, out color);
    }
}
