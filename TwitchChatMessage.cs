using System.Text;

namespace TwitchCorpse
{
    public class TwitchChatMessage
    {
        public class Command
        {
            private readonly string m_Name;
            private readonly string m_Channel;
            private string m_BotCommand = string.Empty;
            private string m_BotCommandParams = string.Empty;
            private readonly bool m_IsCapRequestEnabled;

            public Command(string name, string channel = "", bool isCapRequestEnabled = false)
            {
                m_Name = name;
                m_Channel = channel;
                m_IsCapRequestEnabled = isCapRequestEnabled;
            }

            internal void SetBotCommand(string command, string param)
            {
                m_BotCommand = command;
                m_BotCommandParams = param;
            }

            public string Name => m_Name;
            public string Channel => m_Channel;
            public bool IsCapRequestEnabled => m_IsCapRequestEnabled;
            public string BotCommand => m_BotCommand;
            public string BotCommandParameters => m_BotCommandParams;
        }

        public class SimpleEmote : IComparable<SimpleEmote>
        {
            private readonly string m_ID;
            private readonly int m_Start;
            private readonly int m_End;

            public SimpleEmote(string id, int start, int end)
            {
                m_ID = id;
                m_Start = start;
                m_End = end;
            }

            public int CompareTo(SimpleEmote? comparePart)
            {
                if (comparePart == null || m_Start > comparePart.m_End)
                    return 1;
                return -1;
            }

            public string ID => m_ID;
            public int Start => m_Start;
            public int End => m_End;
        }

        public class Emote
        {
            private readonly List<Tuple<int, int>> m_Locations = new();
            private readonly string m_ID;

            public Emote(string iD) => m_ID = iD;

            internal void AddLocation(int start, int end) => m_Locations.Add(new(start, end));

            public string ID => m_ID;
            public List<Tuple<int, int>> Locations => m_Locations;
            public bool HaveLocations => m_Locations.Count != 0;

            internal string ToTagStr()
            {
                StringBuilder builder = new();
                builder.Append(m_ID);
                builder.Append(':');
                int i = 0;
                foreach (Tuple<int, int> location in m_Locations)
                {
                    if (i != 0)
                        builder.Append(',');
                    builder.Append(location.Item1);
                    builder.Append('-');
                    builder.Append(location.Item2);
                    ++i;
                }
                return builder.ToString();
            }
        }

        public class Tags
        {
            private readonly Dictionary<string, string> m_Tags = new();
            private readonly Dictionary<string, string> m_Badges = new();
            private readonly Dictionary<string, string> m_BadgeInfos = new();
            private readonly Dictionary<string, Emote> m_Emotes = new();
            private readonly List<SimpleEmote> m_OrderedEmotes = new();
            private readonly HashSet<string> m_EmotesSets = new();

            public string[] Badges => m_Badges.Keys.ToArray();
            public HashSet<string> EmotesSets => m_EmotesSets;
            public List<SimpleEmote> OrderedEmotes => m_OrderedEmotes;

            internal bool HaveTag(string tag) => m_Tags.ContainsKey(tag);
            internal bool HaveBadge(string badge) => m_Badges.ContainsKey(badge);

            internal string GetTag(string tag)
            {
                if (m_Tags.TryGetValue(tag, out var value))
                    return value;
                return string.Empty;
            }

            internal string GetBadgeVersion(string badge)
            {
                if (m_Badges.TryGetValue(badge, out var version))
                    return version;
                return string.Empty;
            }

            internal void AddTag(string tag, string value) => m_Tags[tag] = value;
            internal void AddBadge(string badge, string value) => m_Badges[badge] = value;
            internal void AddBadgeVersion(string badge, string version) => m_BadgeInfos[badge] = version;
            internal void AddEmoteSet(string id) => m_EmotesSets.Add(id);
            internal void AddEmote(string id, int start, int end)
            {
                m_OrderedEmotes.Add(new(id, start, end));
                m_OrderedEmotes.Sort();
                Emote emote = new(id);
                emote.AddLocation(start, end);
                m_Emotes[id] = emote;
            }
            internal string ToTagStr()
            {
                StringBuilder builder = new();
                builder.Append('@');
                builder.Append("badge-info=");
                int i = 0;
                foreach (var badgeInfo in m_BadgeInfos)
                {
                    if (i != 0)
                        builder.Append(',');
                    builder.Append(badgeInfo.Key);
                    builder.Append('/');
                    builder.Append(badgeInfo.Value);
                    ++i;
                }
                builder.Append(';');
                builder.Append("badges=");
                i = 0;
                foreach (var badge in m_Badges)
                {
                    if (i != 0)
                        builder.Append(',');
                    builder.Append(badge.Key);
                    builder.Append('/');
                    builder.Append(badge.Value);
                    ++i;
                }
                builder.Append(';');
                foreach (var tag in m_Tags)
                {
                    builder.Append(tag.Key);
                    builder.Append('=');
                    builder.Append(tag.Value);
                    builder.Append(';');
                }
                builder.Append("emote-sets=");
                i = 0;
                foreach (var emoteSetID in m_EmotesSets)
                {
                    if (i != 0)
                        builder.Append(',');
                    builder.Append(emoteSetID);
                    ++i;
                }
                builder.Append(';');
                builder.Append("emote=");
                i = 0;
                foreach (var emote in m_Emotes)
                {
                    if (i != 0)
                        builder.Append(',');
                    builder.Append(emote.Value.ToTagStr());
                    ++i;
                }
                builder.Append(';');
                return builder.ToString();
            }
        }

        private readonly Command m_Command;
        private readonly Tags? m_Tags;
        private readonly string m_Nick;
        private readonly string m_Host;
        private readonly string m_Parameters;

        public TwitchChatMessage(string command, string channel = "", string parameters = "", Tags? tags = null)
        {
            m_Command = new(command, channel);
            m_Tags = tags;
            m_Nick = string.Empty;
            m_Host = string.Empty;
            m_Parameters = parameters;
        }

        internal TwitchChatMessage(Command command, Tags? tags, string nick, string host, string parameters)
        {
            m_Command = command;
            m_Tags = tags;
            m_Nick = nick;
            m_Host = host;
            m_Parameters = parameters;
        }

        public override string ToString()
        {
            StringBuilder builder = new();
            if (m_Tags != null)
            {
                builder.Append(m_Tags.ToTagStr());
                builder.Append(' ');
            }
            builder.Append(m_Command.Name);
            if (!string.IsNullOrWhiteSpace(m_Command.Channel))
            {
                builder.Append(' ');
                builder.Append(m_Command.Channel);
            }
            if (!string.IsNullOrWhiteSpace(m_Parameters))
            {
                builder.Append(' ');
                builder.Append(':');
                builder.Append(m_Parameters);
            }
            builder.Append("\r\n");
            return builder.ToString();
        }

        public string Channel => m_Command.Channel;
        public Command GetCommand() => m_Command;
        public bool HaveTags => m_Tags != null;
        public Tags? GetTags() => m_Tags;
        public string[] GetBadges() => m_Tags?.Badges ?? Array.Empty<string>();
        public string Nick => m_Nick;
        public string Host => m_Host;
        public string Parameters => m_Parameters;

        public List<SimpleEmote> Emotes => m_Tags != null ? m_Tags.OrderedEmotes : new();

        public bool HaveTag(string tag)
        {
            if (m_Tags != null)
                return m_Tags.HaveTag(tag);
            return false;
        }

        public bool HaveBadge(string badge)
        {
            if (m_Tags != null)
                return m_Tags.HaveBadge(badge);
            return false;
        }

        public string GetTag(string tag)
        {
            if (m_Tags != null)
                return m_Tags.GetTag(tag);
            return string.Empty;
        }

        public string GetBadgeVersion(string badge)
        {
            if (m_Tags != null)
                return m_Tags.GetBadgeVersion(badge);
            return string.Empty;
        }

        internal static TwitchChatMessage? Parse(string message, TwitchAPI api)
        {
            string rawTagsComponent = string.Empty;
            string rawSourceComponent = string.Empty;
            string rawParametersComponent = string.Empty;

            if (message[0] == '@')
            {
                int i = message.IndexOf(' ');
                rawTagsComponent = message[1..i];
                message = message[(i + 1)..];
            }

            if (message[0] == ':')
            {
                int i = message.IndexOf(' ');
                rawSourceComponent = message[1..i];
                message = message[(i + 1)..];
            }

            string rawCommandComponent;
            int endIdx = message.IndexOf(':');
            if (endIdx == -1)
                rawCommandComponent = message.Trim();
            else
            {
                rawCommandComponent = message[..endIdx].Trim();
                rawParametersComponent = message[(endIdx + 1)..];
            }

            string[] commandParts = rawCommandComponent.Split(' ');
            Command? command = commandParts[0] switch
            {
                "PING" or "GLOBALUSERSTATE" or "RECONNECT" => new(commandParts[0]),
                "CAP" => new(commandParts[0], isCapRequestEnabled: commandParts[2] == "ACK"),
                "421" => new("UNSUPPORTED", commandParts[2]),
                "001" => new("LOGGED"),
                "353" => new("USERLIST"),
                "JOIN" or "PART" or "NOTICE" or "USERNOTICE" or "CLEARCHAT" or "HOSTTARGET" or "PRIVMSG" or "USERSTATE" or "ROOMSTATE" or _ => new(commandParts[0], commandParts[1]),
            };
            if (command != null)
            {
                Tags? tags = null;
                string nick = string.Empty;
                string host = string.Empty;
                if (!string.IsNullOrWhiteSpace(rawTagsComponent))
                {
                    List<string> tagsToIgnore = new() { "client-nonce", "flags" };
                    tags = new();
                    string[] parsedTags = rawTagsComponent.Split(';');
                    foreach (string tag in parsedTags)
                    {
                        string[] parsedTag = tag.Split('=');
                        string tagName = parsedTag[0];
                        string tagValue = parsedTag[1];
                        if (tagName == "badges" || tagName == "badge-info")
                        {
                            if (!string.IsNullOrWhiteSpace(tagValue))
                            {
                                string[] badges = tagValue.Split(',');
                                foreach (string pair in badges)
                                {
                                    string[] badgeParts = pair.Split('/');
                                    if (tagName == "badges")
                                        tags.AddBadge(badgeParts[0], badgeParts[1]);
                                    else
                                        tags.AddBadgeVersion(badgeParts[0], badgeParts[1]);
                                }
                            }
                        }
                        else if (tagName == "emotes")
                        {
                            if (!string.IsNullOrWhiteSpace(tagValue))
                            {
                                Dictionary<string, string> dictEmotes = new();
                                string[] emotes = tagValue.Split('/');
                                foreach (string emote in emotes)
                                {
                                    string[] emoteParts = emote.Split(':');
                                    string[] positions = emoteParts[1].Split(',');
                                    foreach (string position in positions)
                                    {
                                        string[] positionParts = position.Split('-');
                                        tags.AddEmote(emoteParts[0], int.Parse(positionParts[0]), int.Parse(positionParts[1]));
                                    }
                                }
                            }
                        }
                        else if (tagName == "emote-sets")
                        {
                            string[] emoteSetIds = tagValue.Split(',');
                            foreach (string emoteSetID in emoteSetIds)
                            {
                                tags.AddEmoteSet(emoteSetID);
                                api.LoadEmoteSet(emoteSetID);
                            }
                        }
                        else
                        {
                            if (!tagsToIgnore.Contains(parsedTag[0]))
                                tags.AddTag(parsedTag[0], parsedTag[1]);
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(rawSourceComponent))
                {
                    string[] sourceParts = rawSourceComponent.Split('!');
                    nick = sourceParts.Length == 2 ? sourceParts[0] : string.Empty;
                    host = sourceParts.Length == 2 ? sourceParts[1] : sourceParts[0];
                }
                return new(command, tags, nick, host, rawParametersComponent);
            }
            return null;
        }
    }
}
