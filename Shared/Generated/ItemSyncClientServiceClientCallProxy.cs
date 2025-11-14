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
    public class ItemSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.IItemSyncClientService
    {
        private readonly IClientContext _client;
        public ItemSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnRemoteItemDropped(ItemDropData dropData) => _client.Invoke<DuckyNet.Shared.Services.IItemSyncClientService>("OnRemoteItemDropped", dropData);

        public void OnRemoteItemPickedUp(UInt32 dropId, string pickedByPlayerId) => _client.Invoke<DuckyNet.Shared.Services.IItemSyncClientService>("OnRemoteItemPickedUp", dropId, pickedByPlayerId);

    }
}
