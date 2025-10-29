using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterClientService _impl;
        public CharacterClientServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnPlayerAppearanceUpdated": _impl.OnPlayerAppearanceUpdated((string)args[0], (Byte[])args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
