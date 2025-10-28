using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class RoomServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IRoomService _impl;
        public RoomServiceServerDispatcher(DuckyNet.Shared.Services.IRoomService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "GetRoomListAsync": return _impl.GetRoomListAsync(ctx);
                case "CreateRoomAsync": return _impl.CreateRoomAsync(ctx, (CreateRoomRequest)args[0]);
                case "JoinRoomAsync": return _impl.JoinRoomAsync(ctx, (JoinRoomRequest)args[0]);
                case "LeaveRoomAsync": return _impl.LeaveRoomAsync(ctx);
                case "GetCurrentRoomAsync": return _impl.GetCurrentRoomAsync(ctx);
                case "GetRoomInfoAsync": return _impl.GetRoomInfoAsync(ctx, (string)args[0]);
                case "GetRoomPlayersAsync": return _impl.GetRoomPlayersAsync(ctx, (string)args[0]);
                case "KickPlayerAsync": return _impl.KickPlayerAsync(ctx, (string)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
