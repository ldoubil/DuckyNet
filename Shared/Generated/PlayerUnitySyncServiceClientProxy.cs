using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class PlayerUnitySyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public PlayerUnitySyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void SendPlayerUnitySync(UnitySyncData syncData) => _ctx.Invoke<DuckyNet.Shared.Services.IPlayerUnitySyncService>("SendPlayerUnitySync", syncData);
    }
}
