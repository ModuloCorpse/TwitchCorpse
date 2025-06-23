using CorpseLib.DataNotation;

namespace TwitchCorpse.EventSub.Core
{
    internal class Metadata(DataObject obj)
    {
        private readonly string m_ID = obj.Get<string>("message_id")!;
        private readonly string m_Type = obj.Get<string>("message_type")!;
        private readonly DateTime m_Timestamp = DateTime.Parse(obj.Get<string>("message_timestamp")!);
        private readonly string m_Subscription = obj.GetOrDefault("subscription_type", string.Empty)!;
        private readonly string m_Version = obj.GetOrDefault("subscription_version", string.Empty)!;

        public string ID => m_ID;
        public string Type => m_Type;
        public DateTime Timestamp => m_Timestamp;
        public string Subscription => m_Subscription;
        public string Version => m_Version;
    }
}
