using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class PlayerClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IPlayerClientService _impl;
        public PlayerClientServiceServerDispatcher(DuckyNet.Shared.Services.IPlayerClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnChatMessage": _impl.OnChatMessage((PlayerInfo)args[0], (string)args[1]); return null;
                case "OnPlayerJoined": _impl.OnPlayerJoined((PlayerInfo)args[0]); return null;
                case "OnPlayerLeft": _impl.OnPlayerLeft((PlayerInfo)args[0]); return null;
                case "OnPlayerStatusChanged": _impl.OnPlayerStatusChanged((PlayerInfo)args[0], (PlayerStatus)args[1]); return null;
                case "OnServerMessage": _impl.OnServerMessage((string)args[0], (MessageType)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
