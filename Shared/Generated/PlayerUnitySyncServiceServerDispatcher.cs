using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class PlayerUnitySyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IPlayerUnitySyncService _impl;
        public PlayerUnitySyncServiceServerDispatcher(DuckyNet.Shared.Services.IPlayerUnitySyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "SendPlayerUnitySync": _impl.SendPlayerUnitySync(ctx, (UnitySyncData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
