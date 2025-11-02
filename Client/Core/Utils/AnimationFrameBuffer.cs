using System;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// 动画帧数据 - 存储单帧动画参数
    /// </summary>
    [Serializable]
    public struct AnimationFrame
    {
        // 时间戳
        public double Timestamp;

        // 基础运动参数
        public float MoveSpeed;
        public float MoveDirX;
        public float MoveDirY;

        // 状态标志
        public bool IsDashing;
        public bool IsGunReady;
        public bool IsReloading;
        public bool IsDead;

        // 姿态参数
        public int HandState;
        public int AttackIndex;

        // 动画状态（可选）
        public int StateHash;
        public float NormalizedTime;

        /// <summary>
        /// 检查是否为有效帧
        /// </summary>
        public bool IsValid => Timestamp > 0;

        /// <summary>
        /// 创建空帧
        /// </summary>
        public static AnimationFrame Empty => new AnimationFrame { Timestamp = 0 };
    }

    /// <summary>
    /// 动画帧环形缓冲区 - 使用固定大小数组，零GC
    /// </summary>
    public class AnimationFrameRingBuffer
    {
        private readonly AnimationFrame[] _frames;
        private int _writePos;
        private int _frameCount;

        public int Capacity { get; }
        public int Count => _frameCount;

        /// <summary>
        /// 创建环形缓冲区
        /// </summary>
        /// <param name="capacity">容量（建议 16-64）</param>
        public AnimationFrameRingBuffer(int capacity = 32)
        {
            Capacity = capacity;
            _frames = new AnimationFrame[capacity];
            _writePos = 0;
            _frameCount = 0;
        }

        /// <summary>
        /// 推入新帧
        /// </summary>
        public void Push(AnimationFrame frame)
        {
            _frames[_writePos] = frame;
            _writePos = (_writePos + 1) % Capacity;

            if (_frameCount < Capacity)
            {
                _frameCount++;
            }
        }

        /// <summary>
        /// 获取指定索引的帧（0 = 最旧，Count-1 = 最新）
        /// </summary>
        public AnimationFrame Get(int index)
        {
            if (index < 0 || index >= _frameCount)
            {
                return AnimationFrame.Empty;
            }

            int pos = (_writePos - _frameCount + index + Capacity) % Capacity;
            return _frames[pos];
        }

        /// <summary>
        /// 获取最新的帧
        /// </summary>
        public AnimationFrame GetLatest()
        {
            if (_frameCount == 0)
            {
                return AnimationFrame.Empty;
            }

            return Get(_frameCount - 1);
        }

        /// <summary>
        /// 获取最旧的帧
        /// </summary>
        public AnimationFrame GetOldest()
        {
            if (_frameCount == 0)
            {
                return AnimationFrame.Empty;
            }

            return Get(0);
        }

        /// <summary>
        /// 查找指定时间的帧（使用线性插值）
        /// </summary>
        /// <param name="targetTime">目标时间戳</param>
        /// <returns>插值后的帧</returns>
        public AnimationFrame FindFrameAtTime(double targetTime)
        {
            if (_frameCount == 0)
            {
                return AnimationFrame.Empty;
            }

            // 如果只有一帧，直接返回
            if (_frameCount == 1)
            {
                return Get(0);
            }

            // 查找目标时间所在的两帧
            AnimationFrame frameBefore = AnimationFrame.Empty;
            AnimationFrame frameAfter = AnimationFrame.Empty;

            for (int i = 0; i < _frameCount - 1; i++)
            {
                var current = Get(i);
                var next = Get(i + 1);

                if (current.Timestamp <= targetTime && targetTime <= next.Timestamp)
                {
                    frameBefore = current;
                    frameAfter = next;
                    break;
                }
            }

            // 如果目标时间在最新帧之后，返回最新帧
            if (!frameBefore.IsValid)
            {
                return GetLatest();
            }

            // 如果目标时间在最旧帧之前，返回最旧帧
            if (!frameAfter.IsValid)
            {
                return GetOldest();
            }

            // 线性插值
            return InterpolateFrames(frameBefore, frameAfter, targetTime);
        }

        /// <summary>
        /// 线性插值两帧
        /// </summary>
        private AnimationFrame InterpolateFrames(AnimationFrame a, AnimationFrame b, double targetTime)
        {
            double duration = b.Timestamp - a.Timestamp;
            if (duration <= 0)
            {
                return b;
            }

            float t = (float)((targetTime - a.Timestamp) / duration);
            t = Mathf.Clamp01(t);

            return new AnimationFrame
            {
                Timestamp = targetTime,
                MoveSpeed = Mathf.Lerp(a.MoveSpeed, b.MoveSpeed, t),
                MoveDirX = Mathf.Lerp(a.MoveDirX, b.MoveDirX, t),
                MoveDirY = Mathf.Lerp(a.MoveDirY, b.MoveDirY, t),
                IsDashing = t < 0.5f ? a.IsDashing : b.IsDashing,
                IsGunReady = t < 0.5f ? a.IsGunReady : b.IsGunReady,
                IsReloading = t < 0.5f ? a.IsReloading : b.IsReloading,
                IsDead = t < 0.5f ? a.IsDead : b.IsDead,
                HandState = t < 0.5f ? a.HandState : b.HandState,
                AttackIndex = t < 0.5f ? a.AttackIndex : b.AttackIndex,
                StateHash = t < 0.5f ? a.StateHash : b.StateHash,
                NormalizedTime = Mathf.Lerp(a.NormalizedTime, b.NormalizedTime, t)
            };
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _frameCount = 0;
            _writePos = 0;
        }

        /// <summary>
        /// 移除早于指定时间的帧
        /// </summary>
        public void RemoveOlderThan(double timestamp)
        {
            int removeCount = 0;
            for (int i = 0; i < _frameCount; i++)
            {
                if (Get(i).Timestamp < timestamp)
                {
                    removeCount++;
                }
                else
                {
                    break;
                }
            }

            if (removeCount > 0)
            {
                _frameCount -= removeCount;
            }
        }
    }
}
