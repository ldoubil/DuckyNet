using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class RoomClientServiceBroadcastProxy : DuckyNet.Shared.Services.IRoomClientService
    {
        private readonly object _server;
        public RoomClientServiceBroadcastProxy(object server) => _server = server;

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { "OnPlayerJoinedRoom", new object[] { player, room } });
        }

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { "OnPlayerLeftRoom", new object[] { player, room } });
        }

        public void OnKickedFromRoom(string reason)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { "OnKickedFromRoom", new object[] { reason } });
        }

    }
}
