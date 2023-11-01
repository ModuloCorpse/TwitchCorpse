using CorpseLib.StructuredText;

namespace TwitchCorpse
{
    public interface ITwitchHandler
    {
        public void OnChatMessageRemoved(string messageID);
        public void OnChatUserRemoved(string userID);
        public void OnChatClear();
        public void OnChatMessage(TwitchUser user, bool isHighlight, string messageId, string messageColor, Text message);
        public void OnBits(TwitchUser user, int bits, Text message);
        public void OnChatJoined();
        public void OnUserJoinChat(TwitchUser user);
        public void OnFollow(TwitchUser user);
        public void OnGiftSub(TwitchUser? user, int tier, int nbGift);
        public void OnSub(TwitchUser user, int tier, bool isGift);
        public void OnSharedGiftSub(TwitchUser user, TwitchUser recipient, int tier, int monthGifted, int monthStreak, Text message);
        public void OnSharedSub(TwitchUser user, int tier, int monthTotal, int monthStreak, Text message);
        public void OnReward(TwitchUser user, string reward, string input);
        public void OnRaided(TwitchUser user, int nbViewer);
        public void OnRaiding(TwitchUser user, int nbViewer);
        public void OnStreamStart();
        public void OnStreamStop();

        public void OnMessageHeld(TwitchUser user, string messageID, Text message);
        public void OnHeldMessageTreated(string messageID);

        public void UnhandledEventSub(string message);
    }
}
