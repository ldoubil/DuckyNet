using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterAppearanceClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterAppearanceClientService _impl;
        public CharacterAppearanceClientServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterAppearanceClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnAppearanceReceived": _impl.OnAppearanceReceived((string)args[0], (CharacterAppearanceData)args[1]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
