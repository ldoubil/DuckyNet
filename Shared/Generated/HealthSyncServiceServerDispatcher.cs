using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class HealthSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IHealthSyncService _impl;
        public HealthSyncServiceServerDispatcher(DuckyNet.Shared.Services.IHealthSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "SendHealthSync": _impl.SendHealthSync(ctx, (HealthSyncData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
