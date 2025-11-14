using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class PlayerClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public PlayerClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnChatMessage(PlayerInfo sender, string message) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnChatMessage", sender, message);
        public void OnPlayerJoined(PlayerInfo player) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerJoined", player);
        public void OnPlayerLeft(PlayerInfo player) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerLeft", player);
        public void OnServerMessage(string message, MessageType messageType) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnServerMessage", message, messageType);
        public void OnPlayerUnitySyncReceived(UnitySyncData syncData) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerClientService>("OnPlayerUnitySyncReceived", syncData);
    }
}
