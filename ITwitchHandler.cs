using CorpseLib.StructuredText;
using TwitchCorpse.API;

namespace TwitchCorpse
{
    public interface ITwitchHandler
    {
        public Task OnChatMessageRemoved(string messageID);
        public Task OnChatUserRemoved(string userID);
        public Task OnChatClear();
        public Task OnChatMessage(TwitchChatMessage message);
        public Task OnBits(TwitchUser user, int bits, Text message);
        public Task OnChatJoined();
        public Task OnUserJoinChat(TwitchUser user);
        public Task OnFollow(TwitchUser user);
        public Task OnGiftSub(TwitchUser? user, int tier, int nbGift);
        public Task OnSub(TwitchUser user, int tier, bool isGift);
        public Task OnSharedGiftSub(TwitchUser? gifter, TwitchUser user, int tier, int monthGifted, int monthStreak, Text message);
        public Task OnSharedSub(TwitchUser user, int tier, int monthTotal, int monthStreak, Text message);
        public Task OnRaided(TwitchUser user, int nbViewer);
        public Task OnRaiding(TwitchUser user, int nbViewer);
        public Task OnBeingShoutout(TwitchUser from);
        public Task OnShoutout(TwitchUser moderator, TwitchUser to);
        public Task OnStreamStart();
        public Task OnStreamStop();
        public Task OnMessageHeld(TwitchUser user, string messageID, Text message);
        public Task OnHeldMessageTreated(string messageID);
        public Task OnSharedChatStart();
        public Task OnSharedChatStop();
        public Task OnRewardCreated(TwitchRewardInfo reward);
        public Task OnRewardUpdated(TwitchRewardInfo reward);
        public Task OnRewardDeleted(string rewardID);
        public Task OnRewardClaimed(TwitchUser user, TwitchRewardRedemptionInfo redemption, Text input);
        public Task OnRewardRedemptionStatusChanged(string redemptionID, bool fulfilled);
        public Task UnhandledEventSub(string message);
        public Task OnAdBreakBegin(TimeSpan duration, bool isAutomatic);
    }
}
