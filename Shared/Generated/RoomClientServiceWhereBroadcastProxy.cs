using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class RoomClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IRoomClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public RoomClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerJoinedRoom", new object[] { player, room } });
        }

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerLeftRoom", new object[] { player, room } });
        }

        public void OnKickedFromRoom(string reason)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IRoomClientService));
            method.Invoke(_server, new object[] { _predicate, "OnKickedFromRoom", new object[] { reason } });
        }

    }
}
