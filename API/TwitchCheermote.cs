namespace TwitchCorpse.API
{
    public class TwitchCheermote(string prefix)
    {
        public class Tier(TwitchEmoteImage image, int threshold, bool canCheer) : IComparable<Tier>
        {
            private readonly TwitchEmoteImage m_Image = image;
            private readonly int m_Threshold = threshold;
            private readonly bool m_CanCheer = canCheer;

            public TwitchEmoteImage Image => m_Image;
            public int Threshold => m_Threshold;
            public bool CanCheer => m_CanCheer;

            public int CompareTo(Tier? other) => m_Threshold.CompareTo(other!.m_Threshold);
        }

        private readonly List<Tier> m_TierList = [];
        private readonly string m_Prefix = prefix;

        public Tier[] Tiers => [..m_TierList];
        public string Prefix => m_Prefix;

        public void AddTier(Tier tier)
        {
            if (m_TierList.Count == 0)
            {
                m_TierList.Add(tier);
                return;
            }
            if (m_TierList[^1].CompareTo(tier) <= 0)
            {
                m_TierList.Add(tier);
                return;
            }
            if (m_TierList[0].CompareTo(tier) >= 0)
            {
                m_TierList.Insert(0, tier);
                return;
            }
            int index = m_TierList.BinarySearch(tier);
            if (index < 0)
                index = ~index;
            m_TierList.Insert(index, tier);
        }
    }
}
