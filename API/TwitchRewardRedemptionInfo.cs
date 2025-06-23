namespace TwitchCorpse.API
{
    public class TwitchRewardRedemptionInfo(string redemptionID, string rewardID)
    {
        private readonly string m_RedemptionID = redemptionID;
        private readonly string m_RewardID = rewardID;

        public string RedemptionID => m_RedemptionID;
        public string RewardID => m_RewardID;
    }
}
