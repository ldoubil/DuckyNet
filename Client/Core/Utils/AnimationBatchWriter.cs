using System.Collections.Generic;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 批量动画参数写入器 - 收集一帧内所有参数变化，统一平滑并提交
    /// 使用卡尔曼滤波实现自适应平滑,自动处理网络抖动
    /// </summary>
    public class AnimationBatchWriter
    {
        // 参数缓存
        private readonly Dictionary<int, float> _floatTargets = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> _boolValues = new Dictionary<int, bool>();
        private readonly Dictionary<int, int> _intValues = new Dictionary<int, int>();
        private readonly HashSet<int> _triggerHashes = new HashSet<int>();
        
        // 每个 Float 参数独立的卡尔曼滤波器
        private readonly Dictionary<int, KalmanFilter1D> _floatFilters = new Dictionary<int, KalmanFilter1D>();

        // 滤波配置
        private float _processNoise = 0.01f;     // 过程噪声(运动平滑度)
        private float _measurementNoise = 0.05f; // 测量噪声(网络抖动)

        /// <summary>
        /// 设置平滑参数
        /// </summary>
        public void SetSmoothTime(float smoothTime)
        {
            // 根据平滑时间调整过程噪声(平滑时间越大,过程噪声越小)
            _processNoise = Mathf.Max(0.001f, 0.1f / smoothTime);
        }

        /// <summary>
        /// 设置 Float 参数目标值（会在 Commit 时平滑插值）
        /// </summary>
        public void SetFloat(int hash, float value)
        {
            _floatTargets[hash] = value;
        }

        /// <summary>
        /// 设置 Bool 参数值（立即生效）
        /// </summary>
        public void SetBool(int hash, bool value)
        {
            _boolValues[hash] = value;
        }

        /// <summary>
        /// 设置 Int 参数值（立即生效）
        /// </summary>
        public void SetInt(int hash, int value)
        {
            _intValues[hash] = value;
        }

        /// <summary>
        /// 触发 Trigger（会在 Commit 时触发）
        /// </summary>
        public void FireTrigger(int hash)
        {
            _triggerHashes.Add(hash);
        }

        /// <summary>
        /// 提交所有参数到 Animator
        /// </summary>
        /// <param name="animator">目标 Animator</param>
        /// <param name="deltaTime">帧时间</param>
        public void Commit(Animator animator, float deltaTime)
        {
            if (animator == null) 
            {
                UnityEngine.Debug.LogWarning("[AnimationBatchWriter] Animator 为空，无法提交参数");
                return;
            }

            // Float 参数 - 使用卡尔曼滤波平滑
            foreach (var kv in _floatTargets)
            {
                int hash = kv.Key;
                float target = kv.Value;
                
                // 为每个参数创建独立的卡尔曼滤波器
                if (!_floatFilters.ContainsKey(hash))
                {
                    _floatFilters[hash] = new KalmanFilter1D(_processNoise, _measurementNoise);
                    // 初始化为当前值
                    float current = animator.GetFloat(hash);
                    _floatFilters[hash].Initialize(current);
                }
                
                // 更新卡尔曼滤波器
                var filter = _floatFilters[hash];
                filter.Update(target);
                float smoothed = filter.GetEstimate();
                
                // 应用到 Animator
                animator.SetFloat(hash, smoothed);
            }

            // Bool 参数 - 直接写入
            foreach (var kv in _boolValues)
            {
                animator.SetBool(kv.Key, kv.Value);
            }

            // Int 参数 - 直接写入
            foreach (var kv in _intValues)
            {
                animator.SetInteger(kv.Key, kv.Value);
            }

            // Trigger - 统一触发
            foreach (var hash in _triggerHashes)
            {
                animator.SetTrigger(hash);
            }

            // 清理 Trigger（Float/Bool/Int 保留用于下一帧）
            _triggerHashes.Clear();
        }

        /// <summary>
        /// 清空所有缓存参数
        /// </summary>
        public void Clear()
        {
            _floatTargets.Clear();
            _boolValues.Clear();
            _intValues.Clear();
            _triggerHashes.Clear();
            _floatFilters.Clear(); // 清空滤波器
        }

        /// <summary>
        /// 获取当前缓存的参数数量
        /// </summary>
        public int GetCachedParamCount()
        {
            return _floatTargets.Count + _boolValues.Count + _intValues.Count + _triggerHashes.Count;
        }
    }
}
