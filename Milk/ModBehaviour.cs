using UnityEngine;
using HarmonyLib;

namespace Milk
{
    /// <summary>
    /// Milk 粒子特效模组主行为类
    /// 按 H 键发射牛奶粒子
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {

        /// <summary>
        /// 全局实例
        /// </summary>
        public static ModBehaviour? Instance { get; private set; }

        /// <summary>
        /// Harmony 实例
        /// </summary>
        private static Harmony? _harmony;

        void Awake()
        {
            try
            {
                // 设置全局实例
                Instance = this;

                // 输出模组加载信息
                LogModInfo();

                // 应用 Harmony 补丁
                ApplyHarmonyPatches();
            }
            catch
            {
                // 静默处理异常
            }
        }

        void Update()
        {
            try
            {
                // 按 H 键发射牛奶粒子
                if (Input.GetKeyDown(KeyCode.H))
                {
                    ShootMilkParticles();
                }
            }
            catch
            {
                // 静默处理异常
            }
        }

        void OnDestroy()
        {
            try
            {
                // 移除 Harmony 补丁
                RemoveHarmonyPatches();

                // 清理实例
                Instance = null;
            }
            catch
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 发射牛奶粒子特效
        /// </summary>
        private void ShootMilkParticles()
        {
            try
            {
                // 获取本地玩家的 CharacterMainControl
                CharacterMainControl? characterControl = CharacterMainControl.Main;
                if (characterControl == null) return;

                // 使用粒子发射器发射牛奶粒子
                MilkParticleLauncher.ShootMilkParticles(characterControl);
            }
            catch
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 输出模组信息
        /// </summary>
        private void LogModInfo()
        {
            // 静默加载
        }

        /// <summary>
        /// 应用 Harmony 补丁
        /// </summary>
        private void ApplyHarmonyPatches()
        {
            try
            {
                _harmony = new Harmony("com.duckynet.milk");
                _harmony.PatchAll();
            }
            catch
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 移除 Harmony 补丁
        /// </summary>
        private void RemoveHarmonyPatches()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll(_harmony.Id);
                    _harmony = null;
                }
            }
            catch
            {
                // 静默处理异常
            }
        }
    }
}

