using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 角色客户端服务实现
    /// </summary>
    public class CharacterClientServiceImpl : ICharacterClientService
    {
        public void OnPlayerAppearanceUpdated(string steamId, byte[] appearanceData)
        {
            if (GameContext.IsInitialized && appearanceData != null)
            {
                GameContext.Instance.EventBus.Publish(new PlayerAppearanceUpdatedEvent(steamId, appearanceData));
            }
        }
    }
}
