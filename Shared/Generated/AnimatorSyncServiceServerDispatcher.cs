using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class AnimatorSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IAnimatorSyncService _impl;
        public AnimatorSyncServiceServerDispatcher(DuckyNet.Shared.Services.IAnimatorSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "UpdateAnimatorState": _impl.UpdateAnimatorState(ctx, (AnimatorSyncData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
