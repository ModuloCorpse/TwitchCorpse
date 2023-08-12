using CorpseLib.StructuredText;

namespace TwitchCorpse
{
    public interface ITwitchHandler
    {
        public void OnChatMessage(User user, bool isHighlight, string messageId, string messageColor, Text message);
        public void OnBits(User user, int bits, Text message);
        public void OnChatJoined();
        public void OnUserJoinChat(User user);
        public void OnFollow(User user);
        public void OnGiftSub(User? user, int tier, int nbGift);
        public void OnSub(User user, int tier, bool isGift);
        public void OnSharedSub(User user, int tier, int monthTotal, int monthStreak, Text message);
        public void OnReward(User user, string reward, string input);
        public void OnRaided(User user, int nbViewer);
        public void OnRaiding(User user, int nbViewer);
        public void OnStreamStart();
        public void OnStreamStop();

        public void UnhandledEventSub(string message);
    }
}
