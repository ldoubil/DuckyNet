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
    public class ItemSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public ItemSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<UInt32> DropItemAsync(ItemDropData dropData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IItemSyncService, UInt32>("DropItemAsync", dropData);
        public Task<bool> PickupItemAsync(ItemPickupRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IItemSyncService, bool>("PickupItemAsync", request);
    }
}
