using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class ItemSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IItemSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public ItemSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnRemoteItemDropped(ItemDropData dropData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnRemoteItemDropped", new object[] { dropData } });
        }

        public void OnRemoteItemPickedUp(UInt32 dropId, string pickedByPlayerId)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnRemoteItemPickedUp", new object[] { dropId, pickedByPlayerId } });
        }

    }
}
