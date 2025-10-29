using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterSyncServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterSyncService _impl;
        public CharacterSyncServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterSyncService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "SyncCharacterState": return _impl.SyncCharacterState(ctx, (CharacterSyncData)args[0]);
                case "RequestFullState": return _impl.RequestFullState(ctx);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
