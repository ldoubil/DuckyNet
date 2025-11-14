using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterAppearanceServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterAppearanceService _impl;
        public CharacterAppearanceServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterAppearanceService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "UploadAppearance": _impl.UploadAppearance(ctx, (CharacterAppearanceData)args[0]); return null;
                case "RequestAppearance": _impl.RequestAppearance(ctx, (string)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
