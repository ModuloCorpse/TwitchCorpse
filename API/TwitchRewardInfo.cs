using CorpseLib;
using CorpseLib.DataNotation;

namespace TwitchCorpse.API
{
    public class TwitchRewardInfo
    {
        private readonly TwitchImage m_DefaultImage;
        private readonly TwitchImage? m_Image;
        private readonly DateTime? m_CooldownExpiresAt;
        private readonly string m_ID;
        private readonly string m_Title;
        private readonly string m_Prompt;
        private readonly string m_BackgroundColor;
        private readonly int m_GlobalCooldownSeconds; //-1 is no cooldown
        private readonly int m_MaxPerStream; //-1 is no limit
        private readonly int m_MaxPerUserPerStream; //-1 is no limit
        private readonly int m_Cost;
        private readonly int m_RedemptionsRedeemedCurrentStream;
        private readonly bool m_IsUserInputRequired;
        private readonly bool m_IsEnabled;
        private readonly bool m_IsPaused;
        private readonly bool m_IsInStock;
        private readonly bool m_ShouldRedemptionsSkipRequestQueue;

        public TwitchImage DefaultImage => m_DefaultImage;
        public TwitchImage? Image => m_Image;
        public DateTime? CooldownExpiresAt => m_CooldownExpiresAt;
        public string ID => m_ID;
        public string Title => m_Title;
        public string Prompt => m_Prompt;
        public string BackgroundColor => m_BackgroundColor;
        public int GlobalCooldownSeconds => m_GlobalCooldownSeconds;
        public int MaxPerStream => m_MaxPerStream;
        public int MaxPerUserPerStream => m_MaxPerUserPerStream;
        public int Cost => m_Cost;
        public int RedemptionsRedeemedCurrentStream => m_RedemptionsRedeemedCurrentStream;
        public bool IsUserInputRequired => m_IsUserInputRequired;
        public bool IsEnabled => m_IsEnabled;
        public bool IsPaused => m_IsPaused;
        public bool IsInStock => m_IsInStock;
        public bool ShouldRedemptionsSkipRequestQueue => m_ShouldRedemptionsSkipRequestQueue;

        internal TwitchRewardInfo(TwitchImage defaultImage, TwitchImage? image, DateTime? cooldownExpiresAt, string id, string title, string prompt, string backgroundColor, int globalCooldownSeconds, int maxPerStream, int maxPerUserPerStream, int cost, int redemptionsRedeemedCurrentStream, bool isUserInputRequired, bool isEnabled, bool isPaused, bool isInStock, bool shouldRedemptionsSkipRequestQueue)
        {
            m_DefaultImage = defaultImage;
            m_Image = image;
            m_CooldownExpiresAt = cooldownExpiresAt;
            m_ID = id;
            m_Title = title;
            m_Prompt = prompt;
            m_BackgroundColor = backgroundColor;
            m_GlobalCooldownSeconds = globalCooldownSeconds;
            m_MaxPerStream = maxPerStream;
            m_MaxPerUserPerStream = maxPerUserPerStream;
            m_Cost = cost;
            m_RedemptionsRedeemedCurrentStream = redemptionsRedeemedCurrentStream;
            m_IsUserInputRequired = isUserInputRequired;
            m_IsEnabled = isEnabled;
            m_IsPaused = isPaused;
            m_IsInStock = isInStock;
            m_ShouldRedemptionsSkipRequestQueue = shouldRedemptionsSkipRequestQueue;
        }
    }
}
