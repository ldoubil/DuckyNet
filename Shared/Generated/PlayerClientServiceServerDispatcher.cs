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
                case "OnChatMessage": return _impl.OnChatMessage((PlayerInfo)args[0], (string)args[1]);
                case "OnPlayerJoined": return _impl.OnPlayerJoined((PlayerInfo)args[0]);
                case "OnPlayerLeft": return _impl.OnPlayerLeft((PlayerInfo)args[0]);
                case "OnPlayerStatusChanged": return _impl.OnPlayerStatusChanged((PlayerInfo)args[0], (PlayerStatus)args[1]);
                case "OnServerMessage": return _impl.OnServerMessage((string)args[0], (MessageType)args[1]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
