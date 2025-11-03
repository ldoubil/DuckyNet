using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class ItemSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IItemSyncService _impl;
        public ItemSyncServiceServerDispatcher(DuckyNet.Shared.Services.IItemSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "DropItemAsync": return _impl.DropItemAsync(ctx, (ItemDropData)args[0]);
                case "PickupItemAsync": return _impl.PickupItemAsync(ctx, (ItemPickupRequest)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
