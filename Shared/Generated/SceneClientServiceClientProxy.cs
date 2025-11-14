using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class SceneClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public SceneClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData) => _ctx.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerEnteredScene", playerInfo, scenelData);
        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData) => _ctx.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerLeftScene", playerInfo, scenelData);
    }
}
