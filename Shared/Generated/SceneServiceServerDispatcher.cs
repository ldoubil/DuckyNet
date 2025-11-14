using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class SceneServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ISceneService _impl;
        public SceneServiceServerDispatcher(DuckyNet.Shared.Services.ISceneService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "EnterSceneAsync": return _impl.EnterSceneAsync(ctx, (ScenelData)args[0]);
                case "LeaveSceneAsync": return _impl.LeaveSceneAsync(ctx, (ScenelData)args[0]);
                case "GetScenePlayersAsync": return _impl.GetScenePlayersAsync(ctx, (ScenelData)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
