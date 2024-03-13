using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TwitchCorpse.API
{
    public class TwitchEmoteSet : IEnumerable<TwitchEmoteInfo>
    {
        private readonly Dictionary<string, TwitchEmoteInfo> m_Emotes = [];
        internal void Add(TwitchEmoteInfo info) => m_Emotes[info.ID] = info;
        public bool TryGetEmote(string id, [MaybeNullWhen(false)] out TwitchEmoteInfo? info) => m_Emotes.TryGetValue(id, out info);
        public IEnumerator<TwitchEmoteInfo> GetEnumerator() => ((IEnumerable<TwitchEmoteInfo>)m_Emotes.Values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)m_Emotes.Values).GetEnumerator();
    }
}
