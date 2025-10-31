using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class PlayerClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IPlayerClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public PlayerClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnChatMessage(PlayerInfo sender, string message)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnChatMessage", new object[] { sender, message } });
        }

        public void OnPlayerJoined(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerJoined", new object[] { player } });
        }

        public void OnPlayerLeft(PlayerInfo player)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnPlayerLeft", new object[] { player } });
        }

        public void OnServerMessage(string message, MessageType messageType)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IPlayerClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnServerMessage", new object[] { message, messageType } });
        }

    }
}
