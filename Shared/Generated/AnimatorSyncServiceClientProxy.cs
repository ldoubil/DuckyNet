using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class AnimatorSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public AnimatorSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void UpdateAnimatorState(AnimatorSyncData animatorData) => _ctx.Invoke<DuckyNet.Shared.Services.IAnimatorSyncService>("UpdateAnimatorState", animatorData);
    }
}
