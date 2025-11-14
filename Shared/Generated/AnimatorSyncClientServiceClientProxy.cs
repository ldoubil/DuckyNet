using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class AnimatorSyncClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public AnimatorSyncClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData) => _ctx.Invoke<DuckyNet.Shared.Services.IAnimatorSyncClientService>("OnAnimatorStateUpdated", steamId, animatorData);
    }
}
