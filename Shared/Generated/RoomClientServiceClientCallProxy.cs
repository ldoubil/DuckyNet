using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class RoomClientServiceClientCallProxy : DuckyNet.Shared.Services.IRoomClientService
    {
        private readonly IClientContext _client;
        public RoomClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room) => _client.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnPlayerJoinedRoom", player, room);

        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room) => _client.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnPlayerLeftRoom", player, room);

        public void OnKickedFromRoom(string reason) => _client.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnKickedFromRoom", reason);

    }
}
