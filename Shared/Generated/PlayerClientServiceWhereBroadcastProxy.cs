using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class PlayerClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IPlayerClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public PlayerClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnChatMessage(PlayerInfo sender, string message)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _predicate, "OnChatMessage", new object[] { sender, message } });
        }

        public void OnPlayerJoined(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerJoined", new object[] { player } });
        }

        public void OnPlayerLeft(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerLeft", new object[] { player } });
        }

        public void OnServerMessage(string message, MessageType messageType)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _predicate, "OnServerMessage", new object[] { message, messageType } });
        }

        public void OnPlayerUnitySyncReceived(UnitySyncData syncData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _predicate, "OnPlayerUnitySyncReceived", new object[] { syncData } });
        }

    }
}
