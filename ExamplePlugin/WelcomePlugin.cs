using System;
using System.Linq;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Events;
using DuckyNet.Server.RPC;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;

namespace ExamplePlugin
{
    /// <summary>
    /// 欢迎插件 - 简洁版
    /// 功能：玩家登录时发送欢迎消息
    /// </summary>
    public class WelcomePlugin : IPlugin
    {
        public string Name => "欢迎插件";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "泥嚎！";

        private IPluginContext _context = null!;
        private PlayerInfo _systemPlayer = null!;

        /// <summary>
        /// 插件加载时调用
        /// </summary>
        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info($"{Name} v{Version} 正在加载...");

            // 创建系统消息发送者
            _systemPlayer = new PlayerInfo
            {
                SteamId = "SYSTEM",
                SteamName = "服务器",
                CurrentScenelData = new ScenelData("", "")
            };  

            // 只订阅玩家登录事件
            _context.EventBus.Subscribe<PlayerLoginEvent>(OnPlayerLogin);
            _context.Logger.Info($"{Name} 加载完成！");
        }

        /// <summary>
        /// 插件卸载时调用
        /// </summary>
        public void OnUnload()
        {
            _context.EventBus.Unsubscribe<PlayerLoginEvent>(OnPlayerLogin);
            _context.Logger.Info($"{Name} 已卸载");
        }


        // ========== 事件处理器 ==========

        private void OnPlayerLogin(PlayerLoginEvent e)
        {
            var clientContext = _context.RpcServer.GetClientContext(e.ClientId);
            if (clientContext == null) return;

            // 简洁的欢迎消息
            SendChatToClient(clientContext, $"欢迎来到服务器，{e.Player.SteamName}！🎉");
            
            // 显示在线人数
            var onlineCount = _context.PlayerManager.GetAllOnlinePlayers().Length;
            SendChatToClient(clientContext, $"当前在线: {onlineCount} 人");
        }

        // ========== 辅助方法 ==========

        private void SendChatToClient(IClientContext clientContext, string message)
        {
            clientContext.Call<IPlayerClientService>().OnChatMessage(_systemPlayer, message);
        }
    }
}

