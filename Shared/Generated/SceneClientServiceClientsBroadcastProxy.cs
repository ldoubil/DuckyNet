using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class SceneClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public SceneClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerEnteredScene", new object[] { playerInfo, scenelData } });
        }

        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerLeftScene", new object[] { playerInfo, scenelData } });
        }

    }
}
