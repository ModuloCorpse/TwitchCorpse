namespace TwitchCorpse.EventSub
{
    public class TreatedEventBuffer(int bufferSize)
    {
        private readonly List<string> m_Buffer = [];
        private readonly object m_Lock = new();
        private readonly int m_BufferSize = bufferSize;

        public bool PushEventID(string id)
        {
            lock (m_Lock)
            {
                if (m_Buffer.Contains(id))
                    return false;
                m_Buffer.Add(id);
                int delta = m_Buffer.Count - m_BufferSize;
                if (delta > 0)
                    m_Buffer.RemoveRange(0, delta);
                return true;
            }
        }
    }
}
