using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class HealthSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IHealthSyncClientService _impl;
        public HealthSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.IHealthSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnHealthSyncReceived": _impl.OnHealthSyncReceived((HealthSyncData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
