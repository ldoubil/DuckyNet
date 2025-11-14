using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class RoomClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public RoomClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnPlayerJoinedRoom(PlayerInfo player, RoomInfo room) => _ctx.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnPlayerJoinedRoom", player, room);
        public void OnPlayerLeftRoom(PlayerInfo player, RoomInfo room) => _ctx.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnPlayerLeftRoom", player, room);
        public void OnKickedFromRoom(string reason) => _ctx.Invoke<DuckyNet.Shared.Services.IRoomClientService>("OnKickedFromRoom", reason);
    }
}
