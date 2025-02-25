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

        private static bool TryAddAnimatedImageFromFormat(TwitchImage.Format format, Text text, string alt)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddAnimatedImage(format[scale], alt);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryAddImageFromFormat(TwitchImage.Format format, Text text, string alt)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddImage(format[scale], alt);
                        return true;
                    }
                }
            }
            return false;
        }

        private static void AddTwitchImage(TwitchImage image, Text text)
        {
            if (!TryAddAnimatedImageFromFormat(image[TwitchImage.Theme.Type.DARK][TwitchImage.Format.Type.ANIMATED], text, image.Alt))
            {
                if (!TryAddImageFromFormat(image[TwitchImage.Theme.Type.DARK][TwitchImage.Format.Type.STATIC], text, image.Alt))
                {
                    if (!TryAddAnimatedImageFromFormat(image[TwitchImage.Theme.Type.LIGHT][TwitchImage.Format.Type.ANIMATED], text, image.Alt))
                    {
                        if (!TryAddImageFromFormat(image[TwitchImage.Theme.Type.LIGHT][TwitchImage.Format.Type.STATIC], text, image.Alt))
                            text.AddText(image.Alt);
                    }
                }
            }
        }

        private void AddEmoteToMessage(Text chatMessage, DataObject fragment, string text)
        {
            if (fragment.TryGet("emote", out DataObject? emote) && emote != null &&
                emote!.TryGet("id", out string? id) && id != null &&
                emote.TryGet("emote_set_id", out string? emoteSetID) && emoteSetID != null)
            {
                TwitchEmoteInfo? emoteInfo = m_API.GetEmote(emoteSetID, id);
                if (emoteInfo != null)
                    AddTwitchImage(emoteInfo.Image, chatMessage);
                else
                    chatMessage.AddText(text);
            }
        }

        private void AddCheermoteToMessage(Text chatMessage, DataObject fragment, string text)
        {
            if (fragment.TryGet("cheermote", out DataObject? cheermote) &&
                cheermote!.TryGet("tier", out int? tier) &&
                cheermote!.TryGet("prefix", out string? prefix))
            {
                string cheermotePrefix = prefix!.ToLower();
                TwitchCheermote[] cheermotes = m_API.GetTwitchCheermotes();
                foreach (TwitchCheermote twitchCheermote in cheermotes)
                {
                    if (twitchCheermote.Prefix.Equals(cheermotePrefix, StringComparison.CurrentCultureIgnoreCase))
                    {
                        TwitchCheermote.Tier[] tiers = twitchCheermote.Tiers;
                        foreach (TwitchCheermote.Tier cheermoteTier in tiers)
                        {
                            if (cheermoteTier.Threshold == tier)
                            {
                                AddTwitchImage(cheermoteTier.Image, chatMessage);
                                return;
                            }
                        }
                    }
                }
                chatMessage.AddText(text);
            }
        }

        protected Text ConvertFragments(List<DataObject> fragments)
        {
            Text chatMessage = [];
            foreach (DataObject fragment in fragments)
            {
                if (fragment.TryGet("type", out string? type) &&
                    fragment.TryGet("text", out string? text))
                {
                    //TODO improve escape characters
                    string decodedText = text!.Replace("\\u003e", ">").Replace("\\u003c", "<");
//                    string decodedText = System.Text.RegularExpressions.Regex.Unescape(text!);
                    switch (type!)
                    {
                        case "text":
                        {
                            chatMessage.AddText(decodedText);
                            break;
                        }
                        case "cheermote":
                        {
                            AddCheermoteToMessage(chatMessage, fragment, decodedText);
                            break;
                        }
                        case "emote":
                        {
                            AddEmoteToMessage(chatMessage, fragment, decodedText);
                            break;
                        }
                        case "mention":
                        {
                            if (fragment.TryGet("mention", out DataObject? mention) &&
                                mention!.TryGet("user_name", out string? userName))
                                chatMessage.AddText(string.Format("@{0}", userName));
                            break;
                        }
                    }
                }
            }
            return chatMessage;
        }

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
