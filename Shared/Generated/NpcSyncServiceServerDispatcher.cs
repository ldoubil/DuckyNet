using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class NpcSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.INpcSyncService _impl;
        public NpcSyncServiceServerDispatcher(DuckyNet.Shared.Services.INpcSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "NotifyNpcSpawned": return _impl.NotifyNpcSpawned(ctx, (NpcSpawnData)args[0]);
                case "NotifyNpcTransform": return _impl.NotifyNpcTransform(ctx, (NpcTransformData)args[0]);
                case "NotifyNpcBatchTransform": return _impl.NotifyNpcBatchTransform(ctx, (NpcBatchTransformData)args[0]);
                case "NotifyNpcDestroyed": return _impl.NotifyNpcDestroyed(ctx, (NpcDestroyData)args[0]);
                case "RequestSceneNpcs": return _impl.RequestSceneNpcs(ctx, (string)args[0], (string)args[1]);
                case "RequestSingleNpc": return _impl.RequestSingleNpc(ctx, (string)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
