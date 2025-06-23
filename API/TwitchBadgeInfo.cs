using CorpseLib;
using CorpseLib.DataNotation;

namespace TwitchCorpse.API
{
    public class TwitchBadgeInfo(string id, TwitchImage badgeImage, string title, string description, string clickAction, string clickURL)
    {
        public class DataSerializer : ADataSerializer<TwitchBadgeInfo>
        {
            protected override OperationResult<TwitchBadgeInfo> Deserialize(DataObject reader)
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
                    TwitchImage badgeImage = new();
                    badgeImage[1] = url1x!;
                    badgeImage[2] = url2x!;
                    badgeImage[4] = url4x!;
                    return new(new(id!, badgeImage, title!, description!, clickAction!, clickURL ?? string.Empty));
                }
                return new("Bad json", string.Empty);
            }

            protected override void Serialize(TwitchBadgeInfo obj, DataObject writer)
            {
                writer["id"] = obj.m_ID;
                writer["image_url_1x"] = obj.m_Image[1];
                writer["image_url_2x"] = obj.m_Image[2];
                writer["image_url_4x"] = obj.m_Image[4];
                writer["title"] = obj.m_Title;
                writer["description"] = obj.m_Description;
                writer["click_action"] = obj.m_ClickAction;
                writer["click_url"] = obj.m_ClickURL;
            }
        }

        private readonly TwitchImage m_Image = badgeImage;
        private readonly string m_ID = id;
        private readonly string m_Title = title;
        private readonly string m_Description = description;
        private readonly string m_ClickAction = clickAction;
        private readonly string m_ClickURL = clickURL;

        public TwitchImage Image => m_Image;
        public string ID => m_ID;
        public string Title => m_Title;
        public string Description => m_Description;
        public string ClickAction => m_ClickAction;
        public string ClickURL => m_ClickURL;
    }
}
