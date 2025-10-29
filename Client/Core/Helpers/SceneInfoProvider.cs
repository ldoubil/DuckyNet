using DuckyNet.Client.Patches;

namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// 场景信息提供者 - 提供统一的场景查询接口
    /// </summary>
    public static class SceneInfoProvider
    {
        private static SceneListener? _sceneListener;

        /// <summary>
        /// 初始化场景信息提供者（由 SceneManager 调用）
        /// </summary>
        public static void Initialize(SceneListener sceneListener)
        {
            _sceneListener = sceneListener;
        }

        /// <summary>
        /// 获取当前关卡信息（场景名称）
        /// </summary>
        public static string? GetCurrentLevelInfo()
        {
            return _sceneListener?.GetCurrentLevelInfo();
        }

        /// <summary>
        /// 获取当前地图名称（别名方法）
        /// </summary>
        public static string? GetCurrentMapName()
        {
            return GetCurrentLevelInfo();
        }
    }
}

