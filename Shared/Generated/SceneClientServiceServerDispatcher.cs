using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
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
                case "OnPlayerEnteredScene": _impl.OnPlayerEnteredScene((PlayerInfo)args[0], (ScenelData)args[1]); return null;
                case "OnPlayerLeftScene": _impl.OnPlayerLeftScene((PlayerInfo)args[0], (ScenelData)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
