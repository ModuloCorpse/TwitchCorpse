using CorpseLib.Json;
using CorpseLib.StructuredText;
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

        protected override JObject GenerateSubscriptionCondition(string channelID) => new()
        {
            { "broadcaster_user_id", channelID },
            { "user_id", channelID }
        };

        private bool TryAddAnimatedImageFromFormat(TwitchImage.Format format, Text text)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddAnimatedImage(format[scale]);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryAddImageFromFormat(TwitchImage.Format format, Text text)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddImage(format[scale]);
                        return true;
                    }
                }
            }
            return false;
        }

        private void AddTwitchImage(TwitchImage image, Text text)
        {
            if (!TryAddAnimatedImageFromFormat(image[TwitchImage.Theme.Type.DARK][TwitchImage.Format.Type.ANIMATED], text))
            {
                if (!TryAddImageFromFormat(image[TwitchImage.Theme.Type.DARK][TwitchImage.Format.Type.STATIC], text))
                {
                    if (!TryAddAnimatedImageFromFormat(image[TwitchImage.Theme.Type.LIGHT][TwitchImage.Format.Type.ANIMATED], text))
                    {
                        if (!TryAddImageFromFormat(image[TwitchImage.Theme.Type.LIGHT][TwitchImage.Format.Type.STATIC], text))
                            text.AddText(image.Alt);
                    }
                }
            }
        }

        private void AddEmoteToMessage(Text chatMessage, JObject fragment, string text)
        {
            if (fragment.TryGet("emote", out JObject? emote) &&
                emote!.TryGet("id", out string? id))
            {
                TwitchEmoteInfo? emoteInfo = m_API.GetEmoteFromID(id!);
                if (emoteInfo == null && emote.TryGet("emote_set_id", out string? emoteSetID))
                {
                    m_API.LoadEmoteSet(emoteSetID!);
                    emoteInfo = m_API.GetEmoteFromID(id!);
                }
                if (emoteInfo != null)
                    AddTwitchImage(emoteInfo.Image, chatMessage);
                else
                    chatMessage.AddText(text);
            }
        }

        private void AddCheermoteToMessage(Text chatMessage, JObject fragment, string text)
        {
            if (fragment.TryGet("cheermote", out JObject? cheermote) &&
                cheermote!.TryGet("tier", out int? tier) &&
                cheermote!.TryGet("prefix", out string? prefix))
            {
                string cheermotePrefix = prefix!.ToLower();
                TwitchCheermote[] cheermotes = m_API.GetTwitchCheermotes();
                foreach (TwitchCheermote twitchCheermote in cheermotes)
                {
                    if (twitchCheermote.Prefix.ToLower() == cheermotePrefix)
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
            }
        }

        protected Text ConvertFragments(List<JObject> fragments)
        {
            Text chatMessage = new();
            foreach (JObject fragment in fragments)
            {
                if (fragment.TryGet("type", out string? type) &&
                    fragment.TryGet("text", out string? text))
                {
                    switch (type!)
                    {
                        case "text":
                        {
                            chatMessage.AddText(text!);
                            break;
                        }
                        case "cheermote":
                        {
                            AddCheermoteToMessage(chatMessage, fragment, text!);
                            break;
                        }
                        case "emote":
                        {
                            AddEmoteToMessage(chatMessage, fragment, text!);
                            break;
                        }
                        case "mention":
                        {
                            if (fragment.TryGet("mention", out JObject? mention) &&
                                mention!.TryGet("user_name", out string? userName))
                                chatMessage.AddText(string.Format("@{0}", userName));
                            break;
                        }
                    }
                }
            }
            return chatMessage;
        }

        protected bool ExtractUserInfo(EventData data, out TwitchUser? user, out string? color)
        {
            color = null;
            user = null;
            if (data.TryGet("chatter_user_id", out string? userID))
            {
                user = m_API.GetUserInfoFromID(userID!);
                if (user == null)
                    return false;
                if (!data.TryGet("color", out color) || string.IsNullOrEmpty(color))
                {
                    int colorIdx = 0;
                    foreach (char c in user.Name)
                        colorIdx += c;
                    colorIdx %= ms_Colors.Count;
                    color = ms_Colors[colorIdx];
                }
                return true;
            }
            return false;
        }
    }
}
