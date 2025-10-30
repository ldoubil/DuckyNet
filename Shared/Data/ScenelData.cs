using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 角色同步数据 - 包含位置、旋转和动画状态
    /// </summary>
    [Serializable]
    public class ScenelData
    {
        public string SceneName { get; set; } = "";
        public string SubSceneName { get; set; } = "";
        public ScenelData(string sceneName, string subSceneName)
        {
            SceneName = sceneName;
            SubSceneName = subSceneName;
        }

    }
}