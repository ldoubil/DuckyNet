using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
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
                case "EnterSceneAsync": return _impl.EnterSceneAsync(ctx, (string)args[0]);
                case "LeaveSceneAsync": return _impl.LeaveSceneAsync(ctx);
                case "GetScenePlayersAsync": return _impl.GetScenePlayersAsync(ctx, (string)args[0]);
                case "GetCurrentSceneAsync": return _impl.GetCurrentSceneAsync(ctx);
                case "GetAllPlayerScenesAsync": return _impl.GetAllPlayerScenesAsync(ctx);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
