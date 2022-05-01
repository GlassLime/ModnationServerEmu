﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using BombServerEmu_MNR.Src.Protocols.Clients;
using BombServerEmu_MNR.Src.DataTypes;
using BombServerEmu_MNR.Src.Helpers.Extensions;

namespace BombServerEmu_MNR.Src.Services
{
    class GameBrowser
    {
        public BombService service;

        public GameBrowser(string ip, ushort port)
        {
            service = new BombService("gamebrowser", ProtocolType.TCP, false, ip, port, "output.pfx", "1234");
            service.RegisterMethod("startConnect", Connect.StartConnectHandler);
            service.RegisterMethod("timeSyncRequest", Connect.TimeSyncRequestHandler);

            service.RegisterMethod("subscribeGameEvents", null);
            service.RegisterMethod("unSubscribeGameEvents", UnSubscribeGameEventsHandler);
            service.RegisterMethod("listGames", ListGamesHandler);
            service.RegisterMethod("listFakeGames", null);
        }

        void ListGamesHandler(BombService service, SSLClient client, BombXml xml)
        {
            xml.SetMethod("serverGameList");
            var bw = new BinaryWriter(new MemoryStream());
            bw.Write(1);
            bw.Write(new byte[124]);
            xml.AddParam("serverGameListHeader", Convert.ToBase64String(((MemoryStream)bw.BaseStream).ToArray()));
            xml.AddParam("serverGameList", Convert.ToBase64String(new byte[1024]));
            xml.AddParam("gameListTimeOfDeath", Math.Floor((DateTime.UtcNow.AddHours(1) - new DateTime(1970, 1, 1)).TotalSeconds));
            //xml.SetError("noGamesAvailable");
            client.SendXmlData(xml);
        }

        void UnSubscribeGameEventsHandler(BombService service, SSLClient client, BombXml xml)
        {
            xml.SetMethod("unSubscribeGameEvents");
            client.SendXmlData(xml);
        }
    }
}
