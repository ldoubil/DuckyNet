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
    public class EquipmentServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public EquipmentServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task<bool> UpdateEquipmentSlotAsync(EquipmentSlotUpdateRequest request) => _ctx.InvokeAsync<DuckyNet.Shared.Services.IEquipmentService, bool>("UpdateEquipmentSlotAsync", request);
    }
}
