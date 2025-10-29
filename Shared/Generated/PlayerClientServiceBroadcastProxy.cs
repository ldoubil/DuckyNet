using System;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class PlayerClientServiceBroadcastProxy : DuckyNet.Shared.Services.IPlayerClientService
    {
        private readonly object _server;
        public PlayerClientServiceBroadcastProxy(object server) => _server = server;

        public void OnChatMessage(PlayerInfo sender, string message)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { "OnChatMessage", new object[] { sender, message } });
        }

        public void OnPlayerJoined(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { "OnPlayerJoined", new object[] { player } });
        }

        public void OnPlayerLeft(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { "OnPlayerLeft", new object[] { player } });
        }

        public void OnServerMessage(string message, MessageType messageType)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { "OnServerMessage", new object[] { message, messageType } });
        }

    }
}
