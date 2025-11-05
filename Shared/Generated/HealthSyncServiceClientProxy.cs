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
    public class HealthSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public HealthSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void SendHealthSync(HealthSyncData healthData) => _ctx.Invoke<DuckyNet.Shared.Services.IHealthSyncService>("SendHealthSync", healthData);
    }
}
