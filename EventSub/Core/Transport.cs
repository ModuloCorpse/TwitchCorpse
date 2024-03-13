using CorpseLib.Json;

namespace TwitchCorpse.EventSub.Core
{
    internal class Transport
    {
        private readonly string m_Method;
        private readonly string m_SessionID;
        private readonly string m_Callback;
        private readonly string m_Secret;

        public string Method => m_Method;
        public string SessionID => m_SessionID;
        public string Callback => m_Callback;
        public string Secret => m_Secret;

        public Transport(JsonObject obj)
        {
            m_Method = obj.Get<string>("method")!;
            if (m_Method == "websocket")
            {
                m_Callback = string.Empty;
                m_Secret = string.Empty;
                m_SessionID = obj.Get<string>("session_id")!;
            }
            else
            {
                m_SessionID = string.Empty;
                m_Callback = obj.Get<string>("callback")!;
                m_Secret = obj.GetOrDefault("secret", string.Empty)!;
            }
        }
    }
}
