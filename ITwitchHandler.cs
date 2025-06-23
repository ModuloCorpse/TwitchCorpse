using CorpseLib.StructuredText;
using TwitchCorpse.API;

namespace TwitchCorpse
{
    public interface ITwitchHandler
    {
        public void OnChatMessageRemoved(string messageID);
        public void OnChatUserRemoved(string userID);
        public void OnChatClear();
        public void OnChatMessage(TwitchChatMessage message);
        public void OnBits(TwitchUser user, int bits, Text message);
        public void OnChatJoined();
        public void OnUserJoinChat(TwitchUser user);
        public void OnFollow(TwitchUser user);
        public void OnGiftSub(TwitchUser? user, int tier, int nbGift);
        public void OnSub(TwitchUser user, int tier, bool isGift);
        public void OnSharedGiftSub(TwitchUser? gifter, TwitchUser user, int tier, int monthGifted, int monthStreak, Text message);
        public void OnSharedSub(TwitchUser user, int tier, int monthTotal, int monthStreak, Text message);
        public void OnRaided(TwitchUser user, int nbViewer);
        public void OnRaiding(TwitchUser user, int nbViewer);
        public void OnBeingShoutout(TwitchUser from);
        public void OnShoutout(TwitchUser moderator, TwitchUser to);
        public void OnStreamStart();
        public void OnStreamStop();
        public void OnMessageHeld(TwitchUser user, string messageID, Text message);
        public void OnHeldMessageTreated(string messageID);
        public void OnSharedChatStart();
        public void OnSharedChatStop();
        public void OnRewardClaimed(TwitchUser user, TwitchRewardRedemptionInfo redemption, Text input);
        public void OnRewardCreated(TwitchRewardInfo reward);
        public void OnRewardUpdated(TwitchRewardInfo reward);
        public void OnRewardDeleted(string rewardID);
        public void UnhandledEventSub(string message);
    }
}
