using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchCorpse
{
    public class TwitchStreamInfo
    {
        private readonly List<string> m_Tags;
        private readonly TwitchUser m_User;
        private readonly string m_ID;
        private readonly string m_GameID;
        private readonly string m_GameName;
        private readonly string m_Title;
        private readonly string m_Language;
        private readonly string m_ThumbnailURL;
        private readonly int m_ViewerCount;
        private readonly bool m_IsMature;

        public string ID => m_ID;
        public TwitchUser User => m_User;
        public string GameID => m_GameID;
        public string GameName => m_GameName;
        public string Title => m_Title;
        public List<string> Tags => m_Tags;
        public int ViewerCount => m_ViewerCount;
        public string Language => m_Language;
        public string ThumbnailURL => m_ThumbnailURL;
        public bool IsMature => m_IsMature;

        public TwitchStreamInfo(TwitchUser user, List<string> tags, string id, string gameID, string gameName, string title, string language, string thumbnailURL, int viewerCount, bool isMature)
        {
            m_Tags = tags;
            m_User = user;
            m_ID = id;
            m_GameID = gameID;
            m_GameName = gameName;
            m_Title = title;
            m_Language = language;
            m_ThumbnailURL = thumbnailURL;
            m_ViewerCount = viewerCount;
            m_IsMature = isMature;
        }
    }
}
