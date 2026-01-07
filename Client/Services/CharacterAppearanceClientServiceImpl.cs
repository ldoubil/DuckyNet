using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 角色外观客户端服务实现
    /// 接收服务器推送的外观数据并应用到角色
    /// </summary>
    public class CharacterAppearanceClientServiceImpl : ICharacterAppearanceClientService
    {
        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new CharacterAppearanceReceivedEvent(steamId, appearanceData));
            }
        }
    }
}
