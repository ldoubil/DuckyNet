using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class RoomServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public RoomServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<RoomInfo[]> GetRoomListAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, RoomInfo[]>("GetRoomListAsync");
        public Task<RoomOperationResult> CreateRoomAsync(CreateRoomRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, RoomOperationResult>("CreateRoomAsync", request);
        public Task<RoomOperationResult> JoinRoomAsync(JoinRoomRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, RoomOperationResult>("JoinRoomAsync", request);
        public Task<bool> LeaveRoomAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, bool>("LeaveRoomAsync");
        public Task<RoomInfo> GetCurrentRoomAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, RoomInfo>("GetCurrentRoomAsync");
        public Task<RoomInfo> GetRoomInfoAsync(string roomId) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, RoomInfo>("GetRoomInfoAsync", roomId);
        public Task<PlayerInfo[]> GetRoomPlayersAsync(string roomId) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, PlayerInfo[]>("GetRoomPlayersAsync", roomId);
        public Task<bool> KickPlayerAsync(string SteamId) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IRoomService, bool>("KickPlayerAsync", SteamId);
    }
}
