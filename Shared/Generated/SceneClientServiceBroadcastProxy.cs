using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class SceneClientServiceBroadcastProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly object _server;
        public SceneClientServiceBroadcastProxy(object server) => _server = server;

        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { "OnPlayerEnteredScene", new object[] { playerInfo, scenelData } });
        }

        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { "OnPlayerLeftScene", new object[] { playerInfo, scenelData } });
        }

    }
}
