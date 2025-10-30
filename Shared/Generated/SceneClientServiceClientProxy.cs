using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class SceneClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public SceneClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo) => _ctx.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerEnteredScene", playerSceneInfo);
        public void OnPlayerLeftScene(string SteamId, string sceneName) => _ctx.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerLeftScene", SteamId, sceneName);
    }
}
