using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class ItemSyncClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.IItemSyncClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public ItemSyncClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnRemoteItemDropped(ItemDropData dropData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnRemoteItemDropped", new object[] { dropData } });
        }

        public void OnRemoteItemPickedUp(UInt32 dropId, string pickedByPlayerId)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IItemSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnRemoteItemPickedUp", new object[] { dropId, pickedByPlayerId } });
        }

    }
}
