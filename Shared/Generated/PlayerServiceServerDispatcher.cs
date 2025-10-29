using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class PlayerServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IPlayerService _impl;
        public PlayerServiceServerDispatcher(DuckyNet.Shared.Services.IPlayerService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "LoginAsync": return _impl.LoginAsync(ctx, (PlayerInfo)args[0]);
                case "Logout": _impl.Logout(ctx); return null;
                case "SendChatMessage": _impl.SendChatMessage(ctx, (string)args[0]); return null;
                case "UpdatePlayerStatus": _impl.UpdatePlayerStatus(ctx, (PlayerStatus)args[0]); return null;
                case "GetAllOnlinePlayersAsync": return _impl.GetAllOnlinePlayersAsync(ctx);
                case "GetCurrentRoomPlayersAsync": return _impl.GetCurrentRoomPlayersAsync(ctx);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
