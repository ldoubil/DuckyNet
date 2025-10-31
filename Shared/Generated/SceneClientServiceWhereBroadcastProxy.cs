using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class SceneClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.ISceneClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public SceneClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnPlayerEnteredScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerEnteredScene", new object[] { playerInfo, scenelData } });
        }

        public void OnPlayerLeftScene(PlayerInfo playerInfo, ScenelData scenelData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.ISceneClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerLeftScene", new object[] { playerInfo, scenelData } });
        }

    }
}
