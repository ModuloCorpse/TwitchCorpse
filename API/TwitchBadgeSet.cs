using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TwitchCorpse.API
{
    public class TwitchBadgeSet : IEnumerable<TwitchBadgeInfo>
    {
        private readonly Dictionary<string, TwitchBadgeInfo> m_Badges = [];
        internal void Add(TwitchBadgeInfo info) => m_Badges[info.ID] = info;
        public bool TryGetBadge(string id, [MaybeNullWhen(false)] out TwitchBadgeInfo? info) => m_Badges.TryGetValue(id, out info);
        public IEnumerator<TwitchBadgeInfo> GetEnumerator() => ((IEnumerable<TwitchBadgeInfo>)m_Badges.Values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)m_Badges.Values).GetEnumerator();
    }
}
