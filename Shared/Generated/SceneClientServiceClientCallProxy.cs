using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class SceneClientServiceClientCallProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly IClientContext _client;
        public SceneClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData) => _client.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerEnteredScene", playerInfo, scenelData);

        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData) => _client.Invoke<DuckyNet.Shared.Services.ISceneClientService>("OnPlayerLeftScene", playerInfo, scenelData);

    }
}
