using CorpseLib.DataNotation;
using CorpseLib.StructuredText;
using CorpseLib.Web.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchCorpse.API;

namespace TwitchCorpse.EventSub.Subscriptions
{
    public static class SubscriptionHelper
    {
        private static bool TryAddAnimatedImageFromFormat(TwitchEmoteImage.Format format, Text text, string alt)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddAnimatedImage($"url:{format[scale]}", alt);
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool TryAddImageFromFormat(TwitchEmoteImage.Format format, Text text, string alt)
        {
            if (format.HaveURLs())
            {
                for (float scale = 4f; scale != 0; --scale)
                {
                    if (format.Have(scale))
                    {
                        text.AddImage($"url:{format[scale]}", alt);
                        return true;
                    }
                }
            }
            return false;
        }

        private static void AddTwitchImage(TwitchEmoteImage image, Text text)
        {
            if (!TryAddAnimatedImageFromFormat(image[TwitchEmoteImage.Theme.Type.DARK][TwitchEmoteImage.Format.Type.ANIMATED], text, image.Alt))
            {
                if (!TryAddImageFromFormat(image[TwitchEmoteImage.Theme.Type.DARK][TwitchEmoteImage.Format.Type.STATIC], text, image.Alt))
                {
                    if (!TryAddAnimatedImageFromFormat(image[TwitchEmoteImage.Theme.Type.LIGHT][TwitchEmoteImage.Format.Type.ANIMATED], text, image.Alt))
                    {
                        if (!TryAddImageFromFormat(image[TwitchEmoteImage.Theme.Type.LIGHT][TwitchEmoteImage.Format.Type.STATIC], text, image.Alt))
                            text.AddText(image.Alt);
                    }
                }
            }
        }

        private static void AddEmoteToMessage(TwitchAPI api, Text chatMessage, DataObject fragment, string text)
        {
            if (fragment.TryGet("emote", out DataObject? emote) && emote != null &&
                emote!.TryGet("id", out string? id) && id != null &&
                emote.TryGet("emote_set_id", out string? emoteSetID) && emoteSetID != null)
            {
                TwitchEmoteInfo? emoteInfo = api.GetEmote(emoteSetID, id);
                if (emoteInfo != null)
                    AddTwitchImage(emoteInfo.Image, chatMessage);
                else
                    chatMessage.AddText(text);
            }
        }

        private static void AddCheermoteToMessage(TwitchAPI api, Text chatMessage, DataObject fragment, string text)
        {
            if (fragment.TryGet("cheermote", out DataObject? cheermote) &&
                cheermote!.TryGet("tier", out int? tier) &&
                cheermote!.TryGet("prefix", out string? prefix))
            {
                string cheermotePrefix = prefix!.ToLower();
                TwitchCheermote[] cheermotes = api.GetTwitchCheermotes();
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

        public static Text ConvertFragments(TwitchAPI api, List<DataObject> fragments)
        {
            Text chatMessage = [];
            foreach (DataObject fragment in fragments)
            {
                if (fragment.TryGet("type", out string? type) && fragment.TryGet("text", out string? text))
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
                                AddCheermoteToMessage(api, chatMessage, fragment, decodedText);
                                break;
                            }
                        case "emote":
                            {
                                AddEmoteToMessage(api, chatMessage, fragment, decodedText);
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
    }
}
