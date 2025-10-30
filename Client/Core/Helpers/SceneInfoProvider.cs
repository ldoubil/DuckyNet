using DuckyNet.Client.Patches;

namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// 场景信息提供者 - 提供统一的场景查询接口
    /// </summary>
    public static class SceneInfoProvider
    {
        private static SceneEventBridge? _sceneBridge;

        /// <summary>
        /// 初始化场景信息提供者（由 SceneManager 调用）
        /// </summary>
        public static void Initialize(SceneEventBridge sceneBridge)
        {
            _sceneBridge = sceneBridge;
        }

    }
}
