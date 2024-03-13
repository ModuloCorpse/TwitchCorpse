using CorpseLib.Json;

namespace TwitchCorpse.EventSub.Core
{
    internal class Metadata(JsonObject obj)
    {
        private readonly string m_ID = obj.Get<string>("message_id")!;
        private readonly string m_Type = obj.Get<string>("message_type")!;
        private readonly string m_Timestamp = obj.Get<string>("message_timestamp")!;
        private readonly string m_Subscription = obj.GetOrDefault("subscription_type", string.Empty)!;
        private readonly string m_Version = obj.GetOrDefault("subscription_version", string.Empty)!;

        public string ID => m_ID;
        public string Type => m_Type;
        public string Timestamp => m_Timestamp;
        public string Subscription => m_Subscription;
        public string Version => m_Version;
    }
}
