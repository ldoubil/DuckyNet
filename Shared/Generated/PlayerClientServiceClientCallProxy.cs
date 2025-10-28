using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class PlayerClientServiceClientCallProxy : DuckyNet.Shared.Services.IPlayerClientService
    {
        private readonly IClientContext _client;
        public PlayerClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnChatMessage(PlayerInfo sender, string message) => _client.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnChatMessage", sender, message);

        public void OnPlayerJoined(PlayerInfo player) => _client.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerJoined", player);

        public void OnPlayerLeft(PlayerInfo player) => _client.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerLeft", player);

        public void OnPlayerStatusChanged(PlayerInfo player, PlayerStatus status) => _client.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerStatusChanged", player, status);

        public void OnServerMessage(string message, MessageType messageType) => _client.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnServerMessage", message, messageType);

    }
}
