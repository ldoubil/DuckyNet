using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class RoomClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IRoomClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public RoomClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerJoinedRoom", new object[] { player, room } });
        }

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerLeftRoom", new object[] { player, room } });
        }

        public void OnKickedFromRoom(string reason)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnKickedFromRoom", new object[] { reason } });
        }

    }
}
