using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
namespace DuckyNet.Shared.Services.Generated
{
    public class RoomClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IRoomClientService _impl;
        public RoomClientServiceServerDispatcher(DuckyNet.Shared.Services.IRoomClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnPlayerJoinedRoom": _impl.OnPlayerJoinedRoom((PlayerInfo)args[0], (RoomInfo)args[1]); return null;
                case "OnPlayerLeftRoom": _impl.OnPlayerLeftRoom((PlayerInfo)args[0], (RoomInfo)args[1]); return null;
                case "OnKickedFromRoom": _impl.OnKickedFromRoom((string)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
