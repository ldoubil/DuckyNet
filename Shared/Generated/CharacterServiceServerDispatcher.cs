using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
namespace DuckyNet.Shared.Services.Generated
{
    public class CharacterServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.ICharacterService _impl;
        public CharacterServiceServerDispatcher(DuckyNet.Shared.Services.ICharacterService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "UpdateAppearanceAsync": return _impl.UpdateAppearanceAsync(ctx, (Byte[])args[0]);
                case "GetAppearanceAsync": return _impl.GetAppearanceAsync(ctx, (string)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
