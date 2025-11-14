using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class ItemSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IItemSyncClientService _impl;
        public ItemSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.IItemSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnRemoteItemDropped": _impl.OnRemoteItemDropped((ItemDropData)args[0]); return null;
                case "OnRemoteItemPickedUp": _impl.OnRemoteItemPickedUp((UInt32)args[0], (string)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
