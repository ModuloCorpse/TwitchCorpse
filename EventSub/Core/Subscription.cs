using CorpseLib.Json;

namespace TwitchCorpse.EventSub.Core
{
    internal class Subscription
    {
        private readonly Dictionary<string, string> m_Conditions = [];
        private readonly Transport m_Transport;
        private readonly string m_ID;
        private readonly string m_Type;
        private readonly string m_Version;
        private readonly string m_Status;
        private readonly string m_CreatedAt;
        private readonly int m_Cost;

        internal Transport Transport => m_Transport;
        public string ID => m_ID;
        public string Type => m_Type;
        public string Version => m_Version;
        public string Status => m_Status;
        public string CreatedAt => m_CreatedAt;
        public int Cost => m_Cost;

        public Subscription(JsonObject obj)
        {
            m_ID = obj.Get<string>("id")!;
            m_Type = obj.Get<string>("type")!;
            m_Version = obj.Get<string>("version")!;
            m_Status = obj.Get<string>("status")!;
            m_Cost = obj.Get<int>("cost")!;
            m_CreatedAt = obj.Get<string>("created_at")!;
            m_Transport = new(obj.Get<JsonObject>("transport")!);
            JsonObject conditionObject = obj.Get<JsonObject>("condition")!;
            foreach (var pair in conditionObject)
                m_Conditions[pair.Key] = pair.Value.ToString();
        }

        public bool HaveCondition(string condition) => m_Conditions.ContainsKey(condition);
        public string GetCondition(string condition) => m_Conditions[condition];
    }
}
