using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class SceneClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ISceneClientService _impl;
        public SceneClientServiceServerDispatcher(DuckyNet.Shared.Services.ISceneClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnPlayerEnteredScene": _impl.OnPlayerEnteredScene((PlayerSceneInfo)args[0]); return null;
                case "OnPlayerLeftScene": _impl.OnPlayerLeftScene((string)args[0], (string)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
