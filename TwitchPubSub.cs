using CorpseLib.Json;
using CorpseLib.Logging;
using CorpseLib.Network;
using CorpseLib.StructuredText;
using CorpseLib.Web;
using CorpseLib.Web.Http;
using CorpseLib.Web.OAuth;
using System.Diagnostics;

namespace TwitchCorpse
{
    public class TwitchPubSub(TwitchAPI api, Token token, string channelID, ITwitchHandler? twitchHandler) : WebSocketProtocol(new Dictionary<string, string>() { { "Authorization", string.Format("Bearer {0}", token!.AccessToken) } })
    {
        public static readonly Logger PUBSUB = new("[${d}-${M}-${y} ${h}:${m}:${s}.${ms}] ${log}") { new LogInFile("./log/${y}${M}${d}${h}-PubSub.log") };
        public static void StartLogging() => PUBSUB.Start();
        public static void StopLogging() => PUBSUB.Stop();

        private readonly ITwitchHandler? m_TwitchHandler = twitchHandler;
        private readonly TwitchAPI m_API = api;
        private readonly Token m_Token = token;
        private readonly Stopwatch m_Watch = new();
        private readonly string m_ChannelID = channelID;

        private long m_TimeSinceLastPing = 0;
        private long m_TimeBeforeNextPing = (new Random().Next(-15, 15) + 135) * 1000;

        protected override void OnWSOpen(Response response)
        {
            PUBSUB.Log("<= Listening to automod-queue");
            List<string> topics = [string.Format("automod-queue.{0}.{0}", m_ChannelID)];
            JObject json = new()
            {
                { "type", "LISTEN" }
            };
            JObject data = new()
            {
                { "topics", topics },
                { "auth_token", m_Token.AccessToken }
            };
            json.Add("data", data);
            ForceSend(json.ToNetworkString());
            m_Watch.Start();
            _ = RunUpdateInBackground();
        }

        private static Text GetMessage(JObject messageData)
        {
            string messageText = messageData.GetOrDefault("text", string.Empty)!;
            List<JObject> fragmentsObject = messageData.GetList<JObject>("fragments");
            List<string> fragments = [];
            foreach (JObject fragment in fragmentsObject)
            {
                if (fragment.TryGet("text", out string? str))
                    fragments.Add(str!);
            }
            return new(messageText);
        }

        private void HandleAutoModQueueData(JObject json)
        {
            if (json.TryGet("data", out JObject? data))
            {
                if (data!.TryGet("status", out string? status) &&
                    data!.TryGet("message", out JObject? message))
                {
                    if (status == "PENDING" &&
                        message!.TryGet("id", out string? messageID) &&
                        message.TryGet("content", out JObject? messageData) &&
                        message.TryGet("sender", out JObject? messageSender))
                    {
                        if (messageSender!.TryGet("login", out string? login))
                        {
                            TwitchUser? sender = m_API.GetUserInfoFromLogin(login!);
                            if (sender != null)
                                m_TwitchHandler?.OnMessageHeld(sender, messageID!, GetMessage(messageData!));
                        }
                    }
                    else if ((status == "ALLOWED" || status == "DENIED") && message!.TryGet("id", out string? moderatedMessageID))
                        m_TwitchHandler?.OnHeldMessageTreated(moderatedMessageID!);
                }
            }
        }

        protected override void OnWSMessage(string message)
        {
            PUBSUB.Log(string.Format("<= {0}", message));
            JFile receivedEvent = new(message);
            if (receivedEvent.TryGet("type", out string? type))
            {
                switch (type)
                {
                    case "RECONNECT":
                    {
                        Reconnect();
                        break;
                    }
                    case "MESSAGE":
                    {
                        if (receivedEvent.TryGet("data", out JObject? data))
                        {
                            if (data!.TryGet("topic", out string? topic) &&
                                data.TryGet("message", out string? messageDataStr))
                            {
                                JFile messageData = new(messageDataStr!);
                                if (topic!.StartsWith("automod-queue"))
                                    HandleAutoModQueueData(messageData!);
                            }
                        }
                        break;
                    }
                }
            }
        }

        private async Task RunUpdateInBackground()
        {
            var periodicTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (await periodicTimer.WaitForNextTickAsync())
            {
                if (!IsConnected())
                    return;
                m_Watch.Stop();
                m_TimeSinceLastPing += m_Watch.ElapsedMilliseconds;
                if (m_TimeSinceLastPing >= m_TimeBeforeNextPing)
                {
                    PUBSUB.Log("=> PING");
                    ForceSend("{\"type\": \"PING\"}");
                    m_TimeSinceLastPing = 0;
                    m_TimeBeforeNextPing = (new Random().Next(-15, 15) + 135) * 1000; //2"15 +/- 15 seconds
                }
                m_Watch.Restart();
            }
        }

        protected override void OnWSClose(int code, string closeMessage)
        {
            PUBSUB.Log(string.Format("<=[Error] WebSocket closed ({0}): {1}", code, closeMessage));
            m_Watch.Stop();
        }

        protected override void OnClientDisconnected()
        {
            PUBSUB.Log("<= Disconnected");
        }

        protected override void OnDiscardException(Exception exception)
        {
            PUBSUB.Log(exception.ToString());
        }
    }
}
