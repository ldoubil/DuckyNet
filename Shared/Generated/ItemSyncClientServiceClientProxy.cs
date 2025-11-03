using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class ItemSyncClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public ItemSyncClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnRemoteItemDropped(ItemDropData dropData) => _ctx.Invoke<DuckyNet.Shared.Services.IItemSyncClientService>("OnRemoteItemDropped", dropData);
        public void OnRemoteItemPickedUp(UInt32 dropId, string pickedByPlayerId) => _ctx.Invoke<DuckyNet.Shared.Services.IItemSyncClientService>("OnRemoteItemPickedUp", dropId, pickedByPlayerId);
    }
}
