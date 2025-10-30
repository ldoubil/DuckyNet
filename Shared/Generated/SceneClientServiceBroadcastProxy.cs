using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class SceneClientServiceBroadcastProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly object _server;
        public SceneClientServiceBroadcastProxy(object server) => _server = server;

        public void OnPlayerEnteredScene(PlayerSceneInfo playerSceneInfo)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { "OnPlayerEnteredScene", new object[] { playerSceneInfo } });
        }

        public void OnPlayerLeftScene(string SteamId, string sceneName)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { "OnPlayerLeftScene", new object[] { SteamId, sceneName } });
        }

    }
}
