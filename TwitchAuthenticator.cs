using CorpseLib;
using CorpseLib.Web.OAuth;

namespace TwitchCorpse
{
    public class TwitchAuthenticator(string publicKey, string privateKey)
    {
        private readonly List<string> m_Scopes = [
            "bits:read",
            "channel:manage:broadcast",
            "channel:manage:moderators",
            "channel:manage:polls",
            "channel:manage:redemptions",
            "channel:moderate",
            "channel:read:polls",
            "channel:read:redemptions",
            "channel:read:subscriptions",
            "chat:read",
            "chat:edit",
            "moderator:manage:automod",
            "moderator:read:automod_settings",
            "moderator:read:followers",
            "moderator:manage:banned_users",
            "moderator:manage:blocked_terms",
            "moderator:manage:chat_messages",
            "moderator:manage:shoutouts",
            "moderation:read",
            "user:read:email",
            "whispers:read"
        ];

        private string m_PageContent = string.Empty;
        private readonly string m_PublicKey = publicKey;
        private readonly string m_PrivateKey = privateKey;
        private readonly int m_Port = 3000;

        public TwitchAuthenticator(string publicKey, string privateKey, int port) : this(publicKey, privateKey) => m_Port = port;

        public void SetPageContent(string content) => m_PageContent = content;

        public RefreshToken? Authenticate(string browser = "")
        {
            Authenticator authenticator = new("id.twitch.tv", string.Empty, m_Port);
            authenticator.SetPageContent(m_PageContent);
            OperationResult<RefreshToken> result = authenticator.AuthorizationCode([.. m_Scopes], m_PublicKey, m_PrivateKey, browser);
            if (result)
                return result.Result;
            return null;
        }
    }
}
