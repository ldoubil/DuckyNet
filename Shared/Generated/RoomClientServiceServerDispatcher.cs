using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
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
                case "OnPlayerJoinedRoom": return _impl.OnPlayerJoinedRoom((PlayerInfo)args[0], (RoomInfo)args[1]);
                case "OnPlayerLeftRoom": return _impl.OnPlayerLeftRoom((PlayerInfo)args[0], (RoomInfo)args[1]);
                case "OnKickedFromRoom": return _impl.OnKickedFromRoom((string)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
