using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class EquipmentServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IEquipmentService _impl;
        public EquipmentServiceServerDispatcher(DuckyNet.Shared.Services.IEquipmentService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "UpdateEquipmentSlotAsync": return _impl.UpdateEquipmentSlotAsync(ctx, (EquipmentSlotUpdateRequest)args[0]);
                default: throw new Exception("Unknown method");
            }
        }
    }
}
