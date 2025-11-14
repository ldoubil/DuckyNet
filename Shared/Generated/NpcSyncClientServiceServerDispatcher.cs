using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class NpcSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.INpcSyncClientService _impl;
        public NpcSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.INpcSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnNpcSpawned": _impl.OnNpcSpawned((NpcSpawnData)args[0]); return null;
                case "OnNpcBatchTransform": _impl.OnNpcBatchTransform((NpcBatchTransformData)args[0]); return null;
                case "OnNpcDestroyed": _impl.OnNpcDestroyed((NpcDestroyData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
