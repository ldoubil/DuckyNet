using System;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 简化的平滑同步管理器
    /// 🔥 使用简单的 Lerp 插值替代复杂的卡尔曼滤波
    /// 
    /// 优势：
    /// - 代码简单易维护
    /// - 性能开销极低（只需要 Lerp 运算）
    /// - 20Hz 同步频率下效果已经很流畅
    /// - 避免卡尔曼滤波的预测误差和抖动
    /// </summary>
    public class SmoothSyncManager
    {
        // ========== 简化状态 ==========
        private Vector3 _position;           // 当前平滑后的位置
        private Quaternion _rotation;        // 当前平滑后的旋转
        private Vector3 _targetPosition;     // 目标位置（服务器发来的）
        private Quaternion _targetRotation;  // 目标旋转（服务器发来的）
        private Vector3 _velocity;           // 速度（仅用于记录，不做预测）

        // ========== 配置参数 ==========
        private float _snapDistance = 5f;           // 瞬移距离阈值
        private float _positionSmoothSpeed = 15f;   // 位置平滑速度
        private float _rotationSmoothSpeed = 20f;   // 旋转平滑速度

        // ========== 运行时数据 ==========
        private uint _lastSequenceNumber;
        private bool _hasReceivedData = false;

        /// <summary>
        /// 初始化平滑同步管理器
        /// </summary>
        public SmoothSyncManager(Vector3 initialPosition, Quaternion initialRotation)
        {
            _position = initialPosition;
            _targetPosition = initialPosition;
            _rotation = initialRotation;
            _targetRotation = initialRotation;
            _velocity = Vector3.zero;
            _lastSequenceNumber = 0;
            _hasReceivedData = false;
        }

        /// <summary>
        /// 接收新的同步数据 - 简化的平滑插值
        /// </summary>
        public void ReceiveSyncData(UnitySyncData syncData)
        {
            var (posX, posY, posZ) = syncData.GetPosition();
            var (rotX, rotY, rotZ, rotW) = syncData.GetRotation();
            var (velX, velY, velZ) = syncData.GetVelocity();

            Vector3 newPosition = new Vector3(posX, posY, posZ);
            Quaternion newRotation = new Quaternion(rotX, rotY, rotZ, rotW);
            Vector3 newVelocity = new Vector3(velX, velY, velZ);

            // 检测乱序包（序列号倒退）
            if (_hasReceivedData && IsSequenceOlder(syncData.SequenceNumber, _lastSequenceNumber))
            {
                return; // 丢弃乱序包
            }

            // 检测瞬移（传送/场景切换）
            if (_hasReceivedData)
            {
                float distance = Vector3.Distance(_position, newPosition);
                if (distance > _snapDistance)
                {
                    // 瞬移：直接设置位置，不插值
                    _position = newPosition;
                    _targetPosition = newPosition;
                    _rotation = newRotation;
                    _targetRotation = newRotation;
                    _velocity = newVelocity;
                    _lastSequenceNumber = syncData.SequenceNumber;
                    _hasReceivedData = true;
                    return;
                }
            }

            // 🔥 简化逻辑：直接设置目标，让 Update 做平滑插值
            _targetPosition = newPosition;
            _targetRotation = newRotation;
            _velocity = newVelocity;
            
            // 首次接收数据时，立即设置位置
            if (!_hasReceivedData)
            {
                _position = newPosition;
                _rotation = newRotation;
            }

            _lastSequenceNumber = syncData.SequenceNumber;
            _hasReceivedData = true;
        }

        /// <summary>
        /// 判断序列号是否更旧（处理 uint 溢出）
        /// </summary>
        private bool IsSequenceOlder(uint seq1, uint seq2)
        {
            // 处理序列号溢出（wrapping）
            return ((seq2 - seq1) & 0x80000000) == 0 && seq1 != seq2;
        }

        /// <summary>
        /// 平滑插值更新 - 每帧调用
        /// </summary>
        public void Update()
        {
            if (!_hasReceivedData) return;

            // 🔥 简单高效：直接向目标插值
            // 不需要复杂的物理预测，20Hz 同步频率已经足够流畅
        }

        /// <summary>
        /// 应用到 Transform - 使用平滑插值
        /// </summary>
        /// <param name="targetTransform">目标Transform（用于位置）</param>
        /// <param name="rotationTransform">旋转目标Transform（可选，默认与targetTransform相同）</param>
        public void ApplyToTransform(Transform targetTransform, Transform? rotationTransform = null)
        {
            if (targetTransform == null || !_hasReceivedData) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime < 0.0001f) return;

            // 🔥 简化插值：从当前位置向目标位置平滑移动
            float positionLerpFactor = Mathf.Clamp01(_positionSmoothSpeed * deltaTime);
            _position = Vector3.Lerp(_position, _targetPosition, positionLerpFactor);
            targetTransform.position = _position;

            // 旋转平滑
            Transform rotTarget = rotationTransform ?? targetTransform;
            float rotationLerpFactor = Mathf.Clamp01(_rotationSmoothSpeed * deltaTime);
            _rotation = Quaternion.Slerp(_rotation, _targetRotation, rotationLerpFactor);
            rotTarget.rotation = _rotation;
        }

        // ========== Getter 方法 ==========

        /// <summary>
        /// 获取当前平滑后的位置
        /// </summary>
        public Vector3 GetPosition() => _position;

        /// <summary>
        /// 获取当前平滑后的旋转
        /// </summary>
        public Quaternion GetRotation() => _rotation;

        /// <summary>
        /// 获取速度（仅记录，不做预测）
        /// </summary>
        public Vector3 GetVelocity() => _velocity;

        /// <summary>
        /// 获取目标位置（服务器发来的最新位置）
        /// </summary>
        public Vector3 GetTargetPosition() => _targetPosition;

        // ========== 配置方法 ==========

        /// <summary>
        /// 设置瞬移距离阈值（默认 5m）
        /// 超过此距离将直接设置位置，不插值
        /// </summary>
        public void SetSnapDistance(float distance)
        {
            _snapDistance = Mathf.Max(0f, distance);
        }

        /// <summary>
        /// 设置位置平滑速度（默认 15）
        /// 值越大，跟随目标位置越快
        /// </summary>
        public void SetPositionSmoothSpeed(float speed)
        {
            _positionSmoothSpeed = Mathf.Max(0.1f, speed);
        }

        /// <summary>
        /// 设置旋转平滑速度（默认 20）
        /// 值越大，跟随目标旋转越快
        /// </summary>
        public void SetRotationSmoothSpeed(float speed)
        {
            _rotationSmoothSpeed = Mathf.Max(0.1f, speed);
        }

        /// <summary>
        /// 直接设置位置（瞬移）
        /// </summary>
        public void SetPositionDirect(Vector3 position)
        {
            _position = position;
            _targetPosition = position;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// 直接设置旋转（瞬移）
        /// </summary>
        public void SetRotationDirect(Quaternion rotation)
        {
            _rotation = rotation;
            _targetRotation = rotation;
        }

        /// <summary>
        /// 重置同步管理器（用于场景切换等）
        /// </summary>
        public void Reset(Vector3 position, Quaternion rotation)
        {
            _position = position;
            _targetPosition = position;
            _rotation = rotation;
            _targetRotation = rotation;
            _velocity = Vector3.zero;
            _hasReceivedData = false;
        }
    }
}
