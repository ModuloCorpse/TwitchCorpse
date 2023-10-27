using CorpseLib;
using CorpseLib.Json;

namespace TwitchCorpse
{
    public class TwitchBadgeInfo
    {
        public class JSerializer : AJSerializer<TwitchBadgeInfo>
        {
            protected override OperationResult<TwitchBadgeInfo> Deserialize(JObject reader)
            {
                if (reader.TryGet("id", out string? id) &&
                    reader.TryGet("image_url_1x", out string? url1x) &&
                    reader.TryGet("image_url_2x", out string? url2x) &&
                    reader.TryGet("image_url_4x", out string? url4x) &&
                    reader.TryGet("title", out string? title) &&
                    reader.TryGet("description", out string? description) &&
                    reader.TryGet("click_action", out string? clickAction) &&
                    reader.TryGet("click_url", out string? clickURL))
                {
                    return new(new(id!, url1x!, url2x!, url4x!, title!, description!, clickAction!, clickURL ?? string.Empty));
                }
                return new("Bad json", string.Empty);
            }

            protected override void Serialize(TwitchBadgeInfo obj, JObject writer)
            {
                writer["id"] = obj.m_ID;
                writer["image_url_1x"] = obj.m_URL1x;
                writer["image_url_2x"] = obj.m_URL2x;
                writer["image_url_4x"] = obj.m_URL4x;
                writer["title"] = obj.m_Title;
                writer["description"] = obj.m_Description;
                writer["click_action"] = obj.m_ClickAction;
                writer["click_url"] = obj.m_ClickURL;
            }
        }

        private readonly string m_ID;
        private readonly string m_URL1x;
        private readonly string m_URL2x;
        private readonly string m_URL4x;
        private readonly string m_Title;
        private readonly string m_Description;
        private readonly string m_ClickAction;
        private readonly string m_ClickURL;

        public string ID => m_ID;
        public string URL1x => m_URL1x;
        public string URL2x => m_URL2x;
        public string URL4x => m_URL4x;
        public string Title => m_Title;
        public string Description => m_Description;
        public string ClickAction => m_ClickAction;
        public string ClickURL => m_ClickURL;

        public TwitchBadgeInfo(string id, string url1x, string url2x, string url4x, string title, string description, string clickAction, string clickURL)
        {
            m_ID = id;
            m_URL1x = url1x;
            m_URL2x = url2x;
            m_URL4x = url4x;
            m_Title = title;
            m_Description = description;
            m_ClickAction = clickAction;
            m_ClickURL = clickURL;
        }
    }
}
