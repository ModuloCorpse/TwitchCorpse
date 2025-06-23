using CorpseLib;
using CorpseLib.DataNotation;

namespace TwitchCorpse.API
{
    public class TwitchNewRewardInfo(string title, int cost)
    {
        public class DataSerializer : ADataSerializer<TwitchNewRewardInfo>
        {
            protected override OperationResult<TwitchNewRewardInfo> Deserialize(DataObject reader)
            {
                throw new NotImplementedException();
            }

            protected override void Serialize(TwitchNewRewardInfo obj, DataObject writer)
            {
                writer["title"] = obj.m_Title;
                writer["cost"] = obj.m_Cost;
                writer["is_enabled"] = obj.m_IsEnabled;

                if (obj.m_BackgroundColor != null)
                    writer["background_color"] = obj.m_BackgroundColor;

                if (obj.m_IsUserInputRequired)
                {
                    writer["is_user_input_required"] = true;
                    writer["prompt"] = obj.m_Prompt;
                }

                if (obj.m_MaxPerStream != -1)
                {
                    writer["is_max_per_stream_enabled"] = true;
                    writer["max_per_stream"] = obj.m_MaxPerStream;
                }

                if (obj.m_MaxPerUserPerStream != -1)
                {
                    writer["is_max_per_user_per_stream_enabled"] = true;
                    writer["max_per_user_per_stream"] = obj.m_MaxPerUserPerStream;
                }

                if (obj.m_GlobalCooldownSeconds != -1)
                {
                    writer["is_global_cooldown_enabled"] = true;
                    writer["global_cooldown_seconds"] = obj.m_GlobalCooldownSeconds;
                }

                writer["should_redemptions_skip_request_queue"] = obj.m_ShouldRedemptionsSkipRequestQueue;
            }
        }

        public readonly string m_Title = title;
        public string m_Prompt = string.Empty;
        public string? m_BackgroundColor = null;
        public int m_GlobalCooldownSeconds = -1; //-1 is no cooldown
        public int m_MaxPerStream = -1; //-1 is no limit
        public int m_MaxPerUserPerStream = -1; //-1 is no limit
        public readonly int m_Cost = cost;
        public bool m_IsUserInputRequired = false;
        public bool m_IsEnabled = true;
        public bool m_ShouldRedemptionsSkipRequestQueue = false;

        public void SetPrompt(string prompt)
        {
            m_IsUserInputRequired = true;
            m_Prompt = prompt;
        }

        public void SetBackgroundColor(string backgroundColor) => m_BackgroundColor = backgroundColor;
        public void SetEnabled(bool isEnabled) => m_IsEnabled = isEnabled;
        public void SetRedemptionsShouldSkipRequestQueue(bool shouldRedemptionsSkipRequestQueue) => m_ShouldRedemptionsSkipRequestQueue = shouldRedemptionsSkipRequestQueue;
        public void SetGlobalCooldown(int seconds) => m_GlobalCooldownSeconds = seconds;
        public void SetMaxPerStream(int maxPerStream) => m_MaxPerStream = maxPerStream;
        public void SetMaxPerUserPerStream(int maxPerUserPerStream) => m_MaxPerUserPerStream = maxPerUserPerStream;
    }
}
