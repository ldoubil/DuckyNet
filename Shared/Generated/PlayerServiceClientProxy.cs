using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class PlayerServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public PlayerServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<LoginResult> LoginAsync(PlayerInfo playerInfo) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IPlayerService, LoginResult>("LoginAsync", playerInfo);
        public void Logout() => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerService>("Logout");
        public void SendChatMessage(string message) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerService>("SendChatMessage", message);
        public Task<PlayerInfo[]> GetAllOnlinePlayersAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.IPlayerService, PlayerInfo[]>("GetAllOnlinePlayersAsync");
        public Task<PlayerInfo[]> GetCurrentRoomPlayersAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.IPlayerService, PlayerInfo[]>("GetCurrentRoomPlayersAsync");
    }
}
