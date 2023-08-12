using CorpseLib;
using CorpseLib.Web.OAuth;

namespace TwitchCorpse
{
    public class TwitchAuthenticator
    {
        private readonly List<string> m_Scopes = new() {
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
            "moderation:read",
            "user:read:email",
            "whispers:read"
        };

        private readonly string m_PublicKey;
        private readonly string m_PrivateKey;
        private readonly int m_Port = 3000;

        public TwitchAuthenticator(string publicKey, string privateKey)
        {
            m_PublicKey = publicKey;
            m_PrivateKey = privateKey;
        }

        public TwitchAuthenticator(string publicKey, string privateKey, int port) : this(publicKey, privateKey)
        {
            m_Port = port;
        }

        public RefreshToken? Authenticate(string browser = "")
        {
            Authenticator authenticator = new("id.twitch.tv", string.Empty, m_Port);
            OperationResult<RefreshToken> result = authenticator.AuthorizationCode(m_Scopes.ToArray(), m_PublicKey, m_PrivateKey, browser);
            if (result)
                return result.Result;
            return null;
        }
    }
}
