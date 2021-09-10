﻿using AssettoServer.Server;
using NetCoreServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AssettoServer.Network.Http
{
    class ACHttpSession : HttpSession
    {
        public ACServer ACServer { get; }

        public ACHttpSession(ACServer acServer, HttpServer server) : base(server)
        { 
            ACServer = acServer;
        }

        private string IdFromGuid(string guid)
        {
            if (guid != null)
            {
                using (SHA1Managed sha1 = new SHA1Managed())
                {
                    var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes("antarcticfurseal" + guid));
                    StringBuilder sb = new StringBuilder();
                    foreach(byte b in hash)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }

            return null;
        }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            if(request.Method == "GET")
            {
                string requestUrl = request.Url;
                string responseString = null;

                if (requestUrl.Equals("/INFO", StringComparison.OrdinalIgnoreCase))
                {
                    InfoResponse responseObj = new InfoResponse()
                    {
                        Cars = ACServer.Configuration.EntryCars.Select(c => c.Model).Distinct(),
                        Clients = ACServer.ConnectedCars.Count,
                        Country = new string[] { ACServer.GeoParams.Country, ACServer.GeoParams.CountryCode },
                        CPort = ACServer.Configuration.HttpPort,
                        Durations = ACServer.Configuration.Sessions.Select(c => c.Type == 3 ? c.Laps : c.Time),
                        Extra = ACServer.Configuration.HasExtraLap,
                        Inverted = ACServer.Configuration.InvertedGridPositions,
                        Ip = ACServer.GeoParams.Ip,
                        MaxClients = ACServer.Configuration.MaxClients,
                        Name = ACServer.Configuration.Name,
                        Pass = !string.IsNullOrEmpty(ACServer.Configuration.Password),
                        Pickup = true,
                        Pit = false,
                        Session = ACServer.CurrentSession.Id,
                        Port = ACServer.Configuration.UdpPort,
                        SessionTypes = ACServer.Configuration.Sessions.Select(s => s.Id + 1),
                        Timed = false,
                        TimeLeft = (int)ACServer.CurrentSession.TimeLeft.TotalSeconds,
                        TimeOfDay = (int)ACServer.Configuration.SunAngle,
                        Timestamp = ACServer.CurrentTime,
                        TPort = ACServer.Configuration.TcpPort,
                        Track = ACServer.Configuration.Track + (string.IsNullOrEmpty(ACServer.Configuration.TrackConfig) ? null : "-" + ACServer.Configuration.TrackConfig)
                    };

                    responseString = JsonConvert.SerializeObject(responseObj, Formatting.None, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                }
                else if (requestUrl.StartsWith("/JSON", StringComparison.OrdinalIgnoreCase))
                {
                    EntryListResponse responseObj = new EntryListResponse
                    {
                        Cars = ACServer.EntryCars.Select(ec => new EntryListResponseCar { Model = ec.Model, Skin = ec.Skin, IsEntryList = true, DriverName = ec?.Client?.Name, DriverTeam = ec?.Client?.Team, IsConnected = ec.Client != null }).ToList(),

                    };

                    var features = new List<string>();
                    if (ACServer.Configuration.Extra.UseSteamAuth)
                        features.Add("STEAM_TICKET");

                    features.Add("SPECTATING_AWARE");
                    features.Add("LOWER_CLIENTS_SENDING_RATE");
                    //features.Add("WEATHERFX_V1");
                    features.Add("CLIENTS_EXCHANGE_V1");

                    responseObj.Features = features;

                    responseString = JsonConvert.SerializeObject(responseObj);
                }
                //else if (ACServer.Configuration.Extra.EnableServerDetails && requestUrl.StartsWith("/api/details", StringComparison.OrdinalIgnoreCase))
                //{
                //    DetailResponse responseObj = new DetailResponse()
                //    {
                //        Cars = ACServer.Configuration.EntryCars.Select(c => c.Model).Distinct(),
                //        Clients = ACServer.ConnectedCars.Count,
                //        Country = new string[] { ACServer.GeoParams.Country, ACServer.GeoParams.CountryCode },
                //        CPort = ACServer.Configuration.HttpPort,
                //        Durations = ACServer.Configuration.Sessions.Select(c => c.Type == 3 ? c.Laps : c.Time * 60),
                //        Extra = ACServer.Configuration.HasExtraLap,
                //        Inverted = ACServer.Configuration.InvertedGridPositions,
                //        Ip = ACServer.GeoParams.Ip,
                //        MaxClients = ACServer.Configuration.MaxClients,
                //        Name = ACServer.Configuration.Name,
                //        Pass = !string.IsNullOrEmpty(ACServer.Configuration.Password),
                //        Pickup = true,
                //        Pit = false,
                //        Session = ACServer.CurrentSession.Id,
                //        Port = ACServer.Configuration.UdpPort,
                //        SessionTypes = ACServer.Configuration.Sessions.Select(s => s.Id + 1),
                //        Timed = false,
                //        TimeLeft = (int)ACServer.CurrentSession.TimeLeft.TotalSeconds,
                //        TimeOfDay = (int)ACServer.Configuration.SunAngle,
                //        Timestamp = ACServer.CurrentTime,
                //        TPort = ACServer.Configuration.TcpPort,
                //        Track = ACServer.Configuration.Track + (string.IsNullOrEmpty(ACServer.Configuration.TrackConfig) ? null : "-" + ACServer.Configuration.TrackConfig),
                //        Players = new DetailResponsePlayerList
                //        {
                //            Cars = ACServer.EntryCars.Select(ec => new DetailResponseCar {
                //                Model = ec.Model,
                //                Skin = ec.Skin,
                //                IsEntryList = true,
                //                DriverName = ec?.Client?.Name,
                //                DriverTeam = ec?.Client?.Team,
                //                DriverNation = ec?.Client?.NationCode,
                //                IsConnected = ec.Client != null,
                //                ID = IdFromGuid(ec?.Client?.Guid)
                //            }).ToList(),
                //        },
                //        Until = DateTimeOffset.Now.ToUnixTimeSeconds() + (long)ACServer.CurrentSession.TimeLeft.TotalSeconds * 1000,
                //        Content = ACServer.Configuration.ContentConfiguration,
                //        TrackBase = ACServer.Configuration.Track,
                //        City = ACServer.GeoParams.City,
                //        Frequency = ACServer.Configuration.RefreshRateHz,
                //        Assists = new DetailResponseAssists
                //        {
                //            AbsState = ACServer.Configuration.ABSAllowed,
                //            TcState = ACServer.Configuration.TractionControlAllowed,
                //            FuelRate = (int)(ACServer.Configuration.FuelConsumptionRate * 100),
                //            DamageMultiplier = (int)(ACServer.Configuration.MechanicalDamageRate * 100),
                //            TyreWearRate = (int)(ACServer.Configuration.TyreConsumptionRate * 100),
                //            AllowedTyresOut = ACServer.Configuration.AllowedTyresOutCount,
                //            StabilityAllowed = ACServer.Configuration.StabilityAllowed,
                //            AutoclutchAllowed = ACServer.Configuration.AutoClutchAllowed,
                //            TyreBlanketsAllowed = ACServer.Configuration.AllowTyreBlankets,
                //            ForceVirtualMirror = ACServer.Configuration.IsVirtualMirrorForced
                //        },
                //        WrappedPort = ACServer.Configuration.HttpPort,
                //        AmbientTemperature = ACServer.CurrentWeather.BaseTemperatureAmbient,
                //        RoadTemperature = ACServer.CurrentWeather.BaseTemperatureRoad,
                //        CurrentWeatherId = ACServer.CurrentWeather.Graphics,
                //        // TODO change these when dynamic weather is implemented
                //        WindSpeed = ACServer.CurrentWeather.WindBaseSpeedMin,
                //        WindDirection = ACServer.CurrentWeather.WindBaseDirection,
                //        Description = ACServer.Configuration.Extra.ServerDescription
                //    };

                //    responseString = JsonConvert.SerializeObject(responseObj, Formatting.None, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
                //}

                if (responseString != null)
                {
                    SendResponse(Response.MakeGetResponse(responseString, "application/json"));
                }
                else
                    SendResponse(Response.MakeErrorResponse());
            }
        }
    }
}
