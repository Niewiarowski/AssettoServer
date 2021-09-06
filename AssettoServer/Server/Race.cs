﻿using AssettoServer.Network.Packets.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace AssettoServer.Server
{
    public class Race
    {
        public ACServer Server { get; }
        public EntryCar Challenger { get; }
        public EntryCar Challenged { get; }
        public EntryCar Leader { get; private set; }
        public EntryCar Follower { get; private set; }

        public bool HasStarted { get; private set; }
        public bool LineUpRequired { get; }

        private long LastOvertakeTime { get; set; }
        private Vector3 LastLeaderPosition { get; set; }
        private string ChallengerName { get; }
        private string ChallengedName { get; }

        public Race(ACServer server, EntryCar challenger, EntryCar challenged, bool lineUpRequired = true)
        {
            Server = server;
            Challenger = challenger;
            Challenged = challenged;
            LineUpRequired = lineUpRequired;

            ChallengerName = Challenger.Client.Name;
            ChallengedName = Challenged.Client.Name;
        }

        public Task StartAsync()
        {
            if (!HasStarted)
            {
                HasStarted = true;
                _ = Task.Run(RaceAsync);
            }

            return Task.CompletedTask;
        }

        private async Task RaceAsync()
        {
            try
            {
                if(Challenger.Client == null || Challenged.Client == null)
                {
                    SendMessage("Opponent has disconnected.");
                    return;
                }

                if (LineUpRequired && !AreLinedUp())
                {
                    SendMessage("You have 15 seconds to line up.");

                    Task lineUpTimeout = Task.Delay(15000);
                    Task lineUpChecker = Task.Run(async () =>
                    {
                        while (!lineUpTimeout.IsCompleted && !AreLinedUp())
                            await Task.Delay(150);
                    });

                    Task completedTask = await Task.WhenAny(lineUpTimeout, lineUpChecker);
                    if (completedTask == lineUpTimeout)
                    {
                        SendMessage("You did not line up in time. The race has been cancelled.");
                        return;
                    }
                }

                byte signalStage = 0;
                while(signalStage < 3)
                {
                    if(!AreLinedUp())
                    {
                        SendMessage("You went out of line. The race has been cancelled.");
                        return;
                    }

                    if (signalStage == 0)
                        _ = SendTimedMessageAsync("Ready...");
                    else if (signalStage == 1)
                        _ = SendTimedMessageAsync("Set...");
                    else if (signalStage == 2)
                    {
                        _ = SendTimedMessageAsync("Go!");
                        break;
                    }

                    await Task.Delay(1000);
                    signalStage++;
                }

                while (true)
                {
                    if(Challenger.Client == null)
                    {
                        Leader = Challenged;
                        Follower = Challenger;
                        return;
                    }
                    else if(Challenged.Client == null)
                    {
                        Leader = Challenger;
                        Follower = Challenged;
                        return;
                    }

                    UpdateLeader();

                    Vector3 leaderPosition = Leader.Status.Position;
                    if (Vector3.DistanceSquared(LastLeaderPosition, leaderPosition) > 40000)
                    {
                        Console.WriteLine("teleport");
                        Leader = Follower;
                        Follower = Leader;
                        return;
                    }
                    LastLeaderPosition = leaderPosition;

                    if (Vector3.DistanceSquared(Leader.Status.Position, Follower.Status.Position) > 562500)
                    {
                        Console.WriteLine("too far");
                        return;
                    }

                    if(Server.CurrentTime64 - LastOvertakeTime > 60000)
                        return;

                    await Task.Delay(250);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while running race.");
            }
            finally
            {
                FinishRace();
            }
        }

        private void UpdateLeader()
        {
            bool isFirstUpdate = false;
            if (Leader == null)
            {
                LastOvertakeTime = Server.CurrentTime64;
                Leader = Challenger;
                Follower = Challenged;
                LastLeaderPosition = Leader.Status.Position;
                isFirstUpdate = true;
            }

            float challengerAngle = (float)(Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
            if (challengerAngle < 0)
                challengerAngle += 360;
            float challengerRot = Challenger.Status.GetRotationAngle();

            challengerAngle += challengerRot;
            challengerAngle %= 360;

            float challengedAngle = (float)(Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
            if (challengedAngle < 0)
                challengedAngle += 360;
            float challengedRot = Challenged.Status.GetRotationAngle();

            challengedAngle += challengedRot;
            challengedAngle %= 360;

            float challengerSpeed = (float)Math.Max(0.07716061728, Challenger.Status.Velocity.LengthSquared());
            float challengedSpeed = (float)Math.Max(0.07716061728, Challenged.Status.Velocity.LengthSquared());

            float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);

            EntryCar oldLeader = Leader;

            if ((challengerAngle > 90 && challengerAngle < 275) && Leader != Challenger && challengerSpeed > challengedSpeed && distanceSquared < 2500)
            {
                Leader = Challenger;
                Follower = Challenged;
            }
            else if ((challengedAngle > 90 && challengedAngle < 275) && Leader != Challenged && challengedSpeed > challengerSpeed && distanceSquared < 2500)
            {
                Leader = Challenged;
                Follower = Challenger;
            }

            if(oldLeader != Leader)
            {
                if (!isFirstUpdate)
                    SendMessage($"{Leader.Client?.Name} has overtaken {oldLeader.Client?.Name}");

                LastOvertakeTime = Server.CurrentTime64;
                LastLeaderPosition = Leader.Status.Position;
            }
        }

        private void FinishRace()
        {
            Challenger.CurrentRace = null;
            Challenged.CurrentRace = null;

            if (Leader != null)
            {
                string winnerName = Challenger == Leader ? ChallengerName : ChallengedName;
                string loserName = Challenger == Leader ? ChallengedName : ChallengerName;

                Server.BroadcastPacket(new ChatMessage { SessionId = 255, Message = $"{winnerName} just beat {loserName} in a race." });
            }

            Log.Information("Ending race between {0} and {1}.", ChallengerName, ChallengedName);
        }

        private void SendMessage(string message)
        {
            if (Challenger.Client != null)
                SendMessage(Challenger, message);

            if (Challenged.Client != null)
                SendMessage(Challenged, message);
        }

        private bool AreLinedUp()
        {
            float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);
            Console.WriteLine("Distance: {0}", Math.Sqrt(distanceSquared));

            if (!LineUpRequired)
            {
                return distanceSquared <= 900;
            }
            else
            {
                if (distanceSquared > 100)
                    return false;
            }

            float angle = (float)(Math.Atan2(Challenged.Status.Position.X - Challenger.Status.Position.X, Challenged.Status.Position.Z - Challenger.Status.Position.Z) * 180 / Math.PI);
            if (angle < 0)
                angle += 360;
            float challengerRot = Challenger.Status.GetRotationAngle();

            angle += challengerRot;
            angle %= 360;

            Console.WriteLine("Challenger angle: {0}", angle);
            if (!((angle <= 105 && angle >= 75) || (angle >= 255 && angle <= 285)))
                return false;

            angle = (float)(Math.Atan2(Challenger.Status.Position.X - Challenged.Status.Position.X, Challenger.Status.Position.Z - Challenged.Status.Position.Z) * 180 / Math.PI);
            if (angle < 0)
                angle += 360;
            float challengedRot = Challenged.Status.GetRotationAngle();

            angle += challengedRot;
            angle %= 360;

            Console.WriteLine("Challenged angle: {0}", angle);
            if (!((angle <= 105 && angle >= 75) || (angle >= 255 && angle <= 285)))
                return false;

            float challengerDirection = Challenger.Status.GetRotationAngle();
            float challengedDirection = Challenged.Status.GetRotationAngle();

            float anglediff = (challengerDirection - challengedDirection + 180 + 360) % 360 - 180;
            Console.WriteLine("Direction difference: {0}", anglediff);
            if (Math.Abs(anglediff) > 5)
                return false;

            return true;
        }

        private void SendMessage(EntryCar car, string message)
        {
            if (car.Client != null)
                car.Client.SendPacket(new ChatMessage { SessionId = 255, Message = message });
        }

        private async Task SendTimedMessageAsync(string message)
        {
            bool isChallengerHighPing = Challenger.Ping > Challenged.Ping;
            EntryCar highPingCar, lowPingCar;

            if(isChallengerHighPing)
            {
                highPingCar = Challenger;
                lowPingCar = Challenged;
            }
            else
            {
                highPingCar = Challenged;
                lowPingCar = Challenger;
            }

            SendMessage(highPingCar, message);
            await Task.Delay(highPingCar.Ping - lowPingCar.Ping);
            SendMessage(lowPingCar, message);
        }
    }
}
