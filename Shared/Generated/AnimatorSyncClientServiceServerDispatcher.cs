using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class AnimatorSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IAnimatorSyncClientService _impl;
        public AnimatorSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.IAnimatorSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnAnimatorStateUpdated": _impl.OnAnimatorStateUpdated((string)args[0], (AnimatorSyncData)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
