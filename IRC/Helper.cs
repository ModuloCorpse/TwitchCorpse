using CorpseLib.StructuredText;
using static TwitchCorpse.IRC.Message;

namespace TwitchCorpse.IRC
{
    public static class Helper
    {
        public static Text Convert(API api, string message, List<SimpleEmote> emoteList)
        {
            Text ret = new();
            int lastIndex = 0;
            foreach (SimpleEmote emote in emoteList)
            {
                ret.AddText(message[lastIndex..emote.Start]);
                EmoteInfo? emoteInfo = api.GetEmoteFromID(emote.ID);
                if (emoteInfo != null)
                    ret.AddImage(api.GetEmoteURL(emote.ID, false, 3, false));
                lastIndex = emote.End + 1;
            }
            if (lastIndex < message.Length)
                ret.AddText(message[lastIndex..message.Length]);
            return ret;
        }
    }
}
