using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class SceneServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public SceneServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<bool> EnterSceneAsync(ScenelData scenelData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, bool>("EnterSceneAsync", scenelData);
        public Task<bool> LeaveSceneAsync(ScenelData scenelData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, bool>("LeaveSceneAsync", scenelData);
        public Task<PlayerInfo[]> GetScenePlayersAsync(ScenelData scenelData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, PlayerInfo[]>("GetScenePlayersAsync", scenelData);
    }
}
