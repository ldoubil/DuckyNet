using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class ItemSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.IItemSyncClientService
    {
        private readonly object _server;
        public ItemSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnRemoteItemDropped(ItemDropData dropData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { "OnRemoteItemDropped", new object[] { dropData } });
        }

        public void OnRemoteItemPickedUp(UInt32 dropId, string pickedByPlayerId)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { "OnRemoteItemPickedUp", new object[] { dropId, pickedByPlayerId } });
        }

    }
}
