using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterSyncClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterSyncClientService _impl;
        public CharacterSyncClientServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterSyncClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnCharacterStateUpdate": _impl.OnCharacterStateUpdate((CharacterSyncData)args[0]); return null;
                case "OnFullStateUpdate": _impl.OnFullStateUpdate((CharacterSyncData[])args[0]); return null;
                case "OnCharacterLeft": _impl.OnCharacterLeft((string)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
