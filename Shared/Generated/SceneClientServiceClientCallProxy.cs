using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class SceneClientServiceClientCallProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly IClientContext _client;
        public SceneClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo) => _client.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerEnteredScene", playerSceneInfo);

        public void OnPlayerLeftScene(string SteamId, string sceneName) => _client.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerLeftScene", SteamId, sceneName);

    }
}
