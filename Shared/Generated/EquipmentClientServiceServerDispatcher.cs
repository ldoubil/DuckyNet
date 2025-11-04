using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    public class EquipmentClientServiceServerDispatcher
    {
        private readonly DuckyNet.Shared.Services.IEquipmentClientService _impl;
        public EquipmentClientServiceServerDispatcher(DuckyNet.Shared.Services.IEquipmentClientService impl) => _impl = impl;

        public object Dispatch(string method, object[] args, IClientContext ctx)
        {
            switch (method)
            {
                case "OnEquipmentSlotUpdated": _impl.OnEquipmentSlotUpdated((EquipmentSlotUpdateNotification)args[0]); return null;
                case "OnAllPlayersEquipmentReceived": _impl.OnAllPlayersEquipmentReceived((AllPlayersEquipmentData)args[0]); return null;
                default: throw new Exception("Unknown method");
            }
        }
    }
}
