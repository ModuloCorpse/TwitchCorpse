using CorpseLib.StructuredText;

namespace TwitchCorpse.API
{
    public class TwitchChatMessage(TwitchUser broadcaster, TwitchUser user, Text message, string replyID, string messageID, string announcementColor, string messageColor, bool isHighlight)
    {
        private readonly TwitchUser m_Broadcaster = broadcaster;
        private readonly TwitchUser m_User = user;
        private readonly Text m_Message = message;
        private readonly string m_ReplyID = replyID;
        private readonly string m_MessageID = messageID;
        private readonly string m_AnnouncementColor = announcementColor;
        private readonly string m_MessageColor = messageColor;
        private readonly bool m_IsHighlight = isHighlight;

        public TwitchUser Broadcaster => m_Broadcaster;
        public TwitchUser User => m_User;
        public bool IsHighlight => m_IsHighlight;
        public string MessageID => m_MessageID;
        public string ReplyID => m_ReplyID;
        public string AnnouncementColor => m_AnnouncementColor;
        public string MessageColor => m_MessageColor;
        public Text Message => m_Message;
    }
}
