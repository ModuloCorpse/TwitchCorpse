﻿namespace TwitchCorpse.API
{
    public class TwitchStreamInfo(TwitchUser user, List<string> tags, string id, string gameID, string gameName, string title, string language, string thumbnailURL, int viewerCount, bool isMature)
    {
        private readonly List<string> m_Tags = tags;
        private readonly TwitchUser m_User = user;
        private readonly string m_ID = id;
        private readonly string m_GameID = gameID;
        private readonly string m_GameName = gameName;
        private readonly string m_Title = title;
        private readonly string m_Language = language;
        private readonly string m_ThumbnailURL = thumbnailURL;
        private readonly int m_ViewerCount = viewerCount;
        private readonly bool m_IsMature = isMature;
        public string ID => m_ID;
        public TwitchUser User => m_User;
        public string GameID => m_GameID;
        public string GameName => m_GameName;
        public string Title => m_Title;
        public List<string> Tags => m_Tags;
        public int ViewerCount => m_ViewerCount;
        public string Language => m_Language;
        public bool IsMature => m_IsMature;

        public string GetThumbnailURL(uint width, uint height) => m_ThumbnailURL.Replace("{width}", width.ToString()).Replace("{height}", height.ToString());
    }
}
