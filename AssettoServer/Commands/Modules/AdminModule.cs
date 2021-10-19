﻿using AssettoServer.Commands.Attributes;
using AssettoServer.Network.Packets.Outgoing;
using AssettoServer.Network.Packets.Shared;
using AssettoServer.Network.Tcp;
using AssettoServer.Network.Udp;
using AssettoServer.Server;
using AssettoServer.Server.Configuration;
using Humanizer;
using Humanizer.Bytes;
using Qmmands;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace AssettoServer.Commands.Modules
{
    [RequireAdmin]
    public class AdminModule : ACModuleBase
    {
        [Command("kick")]
        public Task KickAsync(ACTcpClient player, [Remainder] string reason = null)
        {
            if (player.SessionId == Context.Client?.SessionId)
                Reply("You cannot kick yourself.");
            if (player.IsAdministrator)
                Reply("You cannot kick an administrator");
            else
            {
                string kickMessage = reason == null ? $"{player.Name} ({player.Guid}) has been kicked." : $"{player.Name} has been kicked for: {reason}.";
                return Context.Server.KickAsync(player, KickReason.None, kickMessage);
            }

            return Task.CompletedTask;
        }

        [Command("ban")]
        public ValueTask BanAsync(ACTcpClient player, [Remainder] string reason = null)
        {
            if (player.SessionId == Context.Client?.SessionId)
                Reply("You cannot ban yourself.");
            else if (player.IsAdministrator)
                Reply("You cannot ban an administrator.");
            else
            {
                string kickMessage = reason == null ? $"{player.Name} has been banned." : $"{player.Name} ({player.Guid}) has been banned for: {reason}.";
                return Context.Server.BanAsync(player, KickReason.Blacklisted, kickMessage);
            }

            return ValueTask.CompletedTask;
        }

        [Command("unban")]
        public async Task UnbanAsync(string guid)
        {
            if (Context.Server.Blacklist.ContainsKey(guid))
            {
                await Context.Server.UnbanAsync(guid);
                Reply($"{guid} has been unbanned.");
            }
            else Reply($"ID {guid} is not banned.");
        }

        [Command("pit")]
        public void TeleportToPits([Remainder] ACTcpClient player)
        {
            EntryCar car = player.EntryCar;

            car.Client.SendCurrentSession();
            car.Client.SendPacket(new ChatMessage { SessionId = 255, Message = "You have been teleported to the pits." });

            if (player.SessionId != Context.Client.SessionId)
                Reply($"{car.Client.Name} has been teleported to the pits.");
        }

        [Command("settime")]
        public void SetTime(float time)
        {
            Context.Server.SetTime(time);
            Broadcast("Time has been set.");
        }

        [Command("settimemult")]
        public void SetTimeMult(float multiplier)
        {
            Context.Server.Configuration.TimeOfDayMultiplier = multiplier;
        }

        [Command("setweather")]
        public void SetWeather(int weatherId)
        {
            var allWeathers = Context.Server.Configuration.Weathers;
            if (weatherId >= 0 && weatherId < allWeathers.Count)
            {
                WeatherConfiguration newWeather = allWeathers[weatherId];
                Context.Server.SetWeather(newWeather);
                Reply("Weather has been set.");
            }
            else Reply("There is no weather with this ID.");
        }

        [Command("setafktime")]
        public void SetAfkTime(int time)
        {
            time = Math.Max(1, time);
            Context.Server.Configuration.Extra.MaxAfkTimeMinutes = time;

            Reply($"Maximum AFK time has been set to {time} minutes.");
        }

        [Command("forcelights")]
        public void ForceLights(string toggle)
        {
            bool forceLights = toggle == "on";
            Context.Server.Configuration.Extra.ForceLights = forceLights;

            Reply($"Lights {(forceLights ? "will" : "will not")} be forced on.");
        }

        [Command("distance")]
        public void SetUpdateRate([Remainder] ACTcpClient player)
        {
            Reply(Vector3.Distance(Context.Client.EntryCar.Status.Position, player.EntryCar.Status.Position).ToString());
        }

        [Command("forcelights")]
        public void ForceLights(string toggle, [Remainder] ACTcpClient player)
        {
            bool forceLights = toggle == "on";
            player.EntryCar.ForceLights = forceLights;

            Reply($"{player.Name}'s lights {(forceLights ? "will" : "will not")} be forced on.");
        }

        [Command("whois")]
        public void WhoIs(ACTcpClient player)
        {
            EntryCar car = player.EntryCar;

            Reply($"IP: {(player.TcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint).Address}\nGUID: {player.Guid}\nPing: {car.Ping}ms");
            Reply($"Position: {car.Status.Position}\nVelocity: {(int)(car.Status.Velocity.Length() * 3.6)}kmh");
        }

        [Command("restrict")]
        public void Restrict(ACTcpClient player, float restrictor, float ballastKg)
        {
            player.SendPacket(new BallastUpdate { SessionId = player.SessionId, BallastKg = ballastKg, Restrictor = restrictor });
            Reply("Restrictor and ballast set.");
        }

        [Command("netstats")]
        public void NetStats()
        {
            ACUdpServer udpServer = Context.Server.UdpServer;
            Reply($"Sent: {udpServer.DatagramsSentPerSecond} packets/s ({ByteSize.FromBytes(udpServer.BytesSentPerSecond).Per(TimeSpan.FromSeconds(1)).Humanize("#.##")})\n" +
                $"Received: {udpServer.DatagramsReceivedPerSecond} packets/s ({ByteSize.FromBytes(udpServer.BytesReceivedPerSecond).Per(TimeSpan.FromSeconds(1)).Humanize("#.##")})");
        }

        [Command("say")]
        public void Say([Remainder] string message)
        {
            Broadcast("Administrator: " + message);
        }
    }
}
