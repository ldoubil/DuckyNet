using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class SceneServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public SceneServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<bool> EnterSceneAsync(string sceneName) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, bool>("EnterSceneAsync", sceneName);
        public Task<bool> LeaveSceneAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, bool>("LeaveSceneAsync");
        public Task<PlayerSceneInfo[]> GetScenePlayersAsync(string sceneName) => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, PlayerSceneInfo[]>("GetScenePlayersAsync", sceneName);
        public Task<PlayerSceneInfo> GetCurrentSceneAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, PlayerSceneInfo>("GetCurrentSceneAsync");
        public Task<PlayerSceneInfo[]> GetAllPlayerScenesAsync() => _ctx.InvokeAsync<DuckyNet.Shared.Services.ISceneService, PlayerSceneInfo[]>("GetAllPlayerScenesAsync");
    }
}
