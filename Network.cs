using System;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

namespace MopsBot
{
    public class Server
    {
        private TcpClient client;
        private TcpListener listener;
        private static readonly int ServerPort = 11000;
        public Dictionary<int, TrackerMessage> WaitingForAck;

        public Server()
        {
            WaitingForAck = new Dictionary<int, TrackerMessage>();
        }

        public async Task StartServer()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0");
            listener = new TcpListener(localAddr, ServerPort);
            listener.Start();

            await WaitForClient();
        }

        public async Task WaitForClient()
        {
            while (true)
            {
                if (client != null && !SocketConnected(client.Client))
                {
                    await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Info, "", $"Client disconnected"));
                    client.Close();
                    client.Dispose();
                    client = null;
                }

                if (client == null)
                {
                    await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Info, "", $"Waiting for client to connect..."));
                    client = await listener.AcceptTcpClientAsync();
                    WaitForMessage();

                    await Task.Delay(1000);
                    await SendDummyMessage();
                    await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Info, "", $"{(client.Client.RemoteEndPoint as IPEndPoint).Address}:{(client.Client.RemoteEndPoint as IPEndPoint).Port} connected!"));
                }

                await Task.Delay(1000);
            }
        }

        public async Task WaitForMessage()
        {
            var clientIP = $"{(client.Client.RemoteEndPoint as IPEndPoint).Address}:{(client.Client.RemoteEndPoint as IPEndPoint).Port}";
            string clientStream = "";
            while (true)
            {
                try
                {
                    clientStream += await ObtainFromClient();
                    var messages = findMessages(clientStream, out clientStream);
                    foreach (var message in messages)
                    {
                        await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Info, "", $"Received message: {message.FullMessage}"));
                        await ReceivedMessage(message);
                    }
                }
                catch (Exception e)
                {
                    await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Critical, "", $"Connection failed", e));
                    if(!SocketConnected(client.Client)){
                        client = null;
                        return;
                    }
                }
            }
        }


        private int eventId = 0;
        private object sendLock = new Object();
        public async Task SendDummyMessage()
        {
            await SendMessage("Hello", true);//StaticBase.BotCommunication.SendMessage("{\"ChannelId\":391204235376590851,\"Embed\":{\"Type\":0,\"Description\":\"Coming up:\\n\\nBusiness leaders discuss enterprise blockchain and COVID's impact on monetization.\\n\\n@IBM @BancoMediolanum @IBMBlockchain @fnality @tradelens @ING_news @ING_news @UnitedHealthGrp @EY_US @prysmeconomics @Prysm @salesforce \\n\\n#ConsensusDistributed\\nhttps://t.co/rY6p0afq7y https://t.co/bn56OuAi9E\",\"Url\":\"https://twitter.com/CoinDesk/status/1260261239231578112\",\"Title\":\"Tweet-Link\",\"Timestamp\":\"2020-05-12T19:31:08+02:00\",\"Color\":{\"RawValue\":41971,\"R\":0,\"G\":163,\"B\":243},\"Image\":{\"Url\":\"http://pbs.twimg.com/media/EX1Y6tKWoAEBx2G.png\",\"ProxyUrl\":null,\"Height\":null,\"Width\":null},\"Video\":null,\"Author\":{\"Name\":\"CoinDesk\",\"Url\":\"https://t.co/voQSwZsxYC\",\"IconUrl\":\"http://pbs.twimg.com/profile_images/875399204218126339/W3zmmuWz_normal.jpg\",\"ProxyIconUrl\":null},\"Footer\":{\"Text\":\"Twitter\",\"IconUrl\":\"https://upload.wikimedia.org/wikipedia/de/thumb/9/9f/Twitter_bird_logo_2012.svg/1259px-Twitter_bird_logo_2012.svg.png\",\"ProxyUrl\":null},\"Provider\":null,\"Thumbnail\":{\"Url\":\"http://pbs.twimg.com/profile_images/875399204218126339/W3zmmuWz_normal.jpg\",\"ProxyUrl\":null,\"Height\":null,\"Width\":null},\"Fields\":[],\"Length\":328},\"Sender\":\"coindesk\",\"Notification\":\"~Tweet Tweet~\"}");
        }

        public async Task SendMessage(string message, bool waitForAck = true)
        {
            int id = eventId++;
            var tMessage = new TrackerMessage(id, message);
            if (waitForAck)
                WaitingForAck[id] = tMessage;

            try
            {
                var stream = client.GetStream();
                lock (sendLock)
                {
                    stream.WriteAsync(Encoding.ASCII.GetBytes(tMessage.FullMessage)).AsTask().Wait();
                }
            }
            catch (Exception e)
            {
                await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Error, "", $"Error on sending message", e));
            }
        }

        public async Task ReceivedMessage(TrackerMessage message)
        {
            if (message.IsAck)
            {
                if (WaitingForAck.ContainsKey(message.AckId))
                {
                    WaitingForAck.Remove(message.AckId);
                    await Program.MopsLog(new Discord.LogMessage(Discord.LogSeverity.Info, "", $"Received awaited ACK {message.AckId}"));
                }
            }
            else
            {
                //ToDo: Handle request
                var args = message.Content.Split("|||");
                var trackerType = Enum.Parse<MopsBot.Data.Tracker.BaseTracker.TrackerType>(args[1]);
                var result = "";
                try
                {
                    switch (args.First())
                    {
                        case "ADD":
                            await StaticBase.Trackers[trackerType].AddTrackerAsync(args[3], ulong.Parse(args[2]), args[4]);
                            result = "Added the tracker successfully";
                            break;
                        case "REMOVE":
                            var worked = await StaticBase.Trackers[trackerType].TryRemoveTrackerAsync(args[2], ulong.Parse(args[3]));
                            result = worked ? "Removed successfully" : "Could not find the tracker";
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception e)
                {
                    result = e.InnerException.Message;
                }
                await SendMessage($"ACKNOWLEDGED REQUEST {message.Id}\n{result}", false);
            }
        }

        private ulong requestId = 0;
        public async Task<string> ObtainFromClient()
        {
            string str;
            NetworkStream stream = client.GetStream();

            while (!stream.DataAvailable)
            {
                await Task.Delay(100);
            }

            byte[] buffer = new byte[1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int numBytesRead;
                while (stream.DataAvailable && (numBytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, numBytesRead);
                }
                str = Encoding.ASCII.GetString(ms.ToArray(), 0, (int)ms.Length);
            }

            return str;
        }

        private List<TrackerMessage> findMessages(string inData, out string outData)
        {
            var messages = new List<TrackerMessage>();
            outData = inData;

            while (true)
            {
                var startIndexA = outData.IndexOf("STARTEVENT");
                var startIndexB = string.Join("", outData.Skip(startIndexA)).IndexOf("\n");
                if (startIndexA >= 0 && startIndexB > 0)
                {
                    var eventId = int.Parse((string.Join("", outData.Skip(startIndexA + 11).TakeWhile(x => x != '\n'))));
                    var endIndex = outData.IndexOf($"ENDEVENT {eventId}");
                    if (endIndex >= 0)
                    {
                        messages.Add(new TrackerMessage(eventId, outData.Replace($"STARTEVENT {eventId}\n", "").Replace($"\nENDEVENT {eventId}", "")));
                        outData = outData.Replace(messages.Last().FullMessage, "");
                        continue;
                    }
                }

                return messages;
            }
        }

        private bool SocketConnected(Socket s)
        {
            bool part1 = s.Poll(1000, SelectMode.SelectRead);
            bool part2 = (s.Available == 0);
            if (part1 && part2)
                return false;
            else
                return true;
        }
    }

    public class TrackerMessage
    {
        public int Id, AckId;
        public string Content;
        public DateTime CreatedAt;
        public bool IsAck;
        public string FullMessage;
        public TrackerMessage(int id, string data)
        {
            Id = id;
            Content = data;
            IsAck = Content.Contains("ACKNOWLEDGED REQUEST ");
            FullMessage = $"STARTEVENT {Id}\n{Content}\nENDEVENT {Id}";
            if (IsAck)
            {
                AckId = int.Parse(Content.Split("ACKNOWLEDGED REQUEST ")[1].Split("\n")[0]);
                Content = Content.Split($"ACKNOWLEDGED REQUEST {AckId}\n").Last();
            }
        }
    }
}