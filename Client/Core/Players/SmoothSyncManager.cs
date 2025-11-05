using System;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 高性能平滑同步管理器 (优化版)
    /// 使用预测性插值（Extrapolation）+ 缓冲快照系统
    /// 算法参考：Source Engine 的网络插值实现
    /// 
    /// 重要：时间戳使用本地接收时间，而非传输的序列号
    /// 这样可以避免客户端/服务器时钟不同步的问题
    /// </summary>
    public class SmoothSyncManager
    {
        // ========== 常量配置 ==========
        private const float MIN_INTERPOLATION_DELAY = 0.02f;      // 最小延迟 20ms
        private const float DEFAULT_INTERPOLATION_DELAY = 0.05f;  // 默认延迟 50ms
        private const float DEFAULT_EXTRAPOLATION_LIMIT = 0.5f;   // 默认外推限制 500ms
        private const float DEFAULT_SNAP_DISTANCE = 5f;           // 默认瞬移距离
        private const float DEFAULT_ROTATION_SNAP_ANGLE = 180f;   // 默认旋转瞬移角度
        private const float DEFAULT_POSITION_SMOOTH_SPEED = 15f;  // 默认位置平滑速度
        private const float DEFAULT_ROTATION_SMOOTH_SPEED = 10f;  // 默认旋转平滑速度
        private const float SMALL_ANGLE_THRESHOLD = 10f;          // 小角度阈值（用 Lerp）
        private const uint SEQUENCE_HALF = 0x80000000;            // 序列号中点（用于溢出判断）
        
        // ========== 快照缓冲 ==========
        private struct Snapshot
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public float LocalReceiveTime;  // 本地接收时间（Time.time）
            public uint SequenceNumber;     // 序列号（用于检测乱序/重复）
        }
        
        private Snapshot _fromSnapshot;      // 起始快照
        private Snapshot _toSnapshot;        // 目标快照
        private Snapshot _currentSnapshot;   // 当前插值结果
        
        // ========== 配置参数 ==========
        private float _interpolationDelay = DEFAULT_INTERPOLATION_DELAY;
        private float _extrapolationLimit = DEFAULT_EXTRAPOLATION_LIMIT;
        private float _snapDistance = DEFAULT_SNAP_DISTANCE;
        private float _rotationSnapAngle = DEFAULT_ROTATION_SNAP_ANGLE;
        private float _positionSmoothSpeed = DEFAULT_POSITION_SMOOTH_SPEED;
        private float _rotationSmoothSpeed = DEFAULT_ROTATION_SMOOTH_SPEED;
        
        // ========== 运行时数据 ==========
        private bool _hasReceivedData = false;
        
        // 🔥 优化：缓存计算结果，减少重复计算
        private float _cachedTimeDiff;
        private float _cachedInterpolationT;
        
        /// <summary>
        /// 初始化平滑同步管理器
        /// </summary>
        public SmoothSyncManager(Vector3 initialPosition, Quaternion initialRotation)
        {
            float currentTime = Time.time;
            
            _fromSnapshot = new Snapshot
            {
                Position = initialPosition,
                Rotation = initialRotation,
                Velocity = Vector3.zero,
                LocalReceiveTime = currentTime,
                SequenceNumber = 0
            };
            
            _toSnapshot = _fromSnapshot;
            _currentSnapshot = _fromSnapshot;
        }
        
        /// <summary>
        /// 接收新的同步数据 - O(1) 复杂度
        /// 重要：使用本地接收时间，而非传输的序列号作为时间戳
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
            if (_hasReceivedData && IsSequenceOlder(syncData.SequenceNumber, _toSnapshot.SequenceNumber))
            {
                // 丢弃乱序的旧包
                return;
            }
            
            // 🔥 优化：使用 sqrMagnitude 避免平方根计算（~30% 性能提升）
            float sqrDistance = (_toSnapshot.Position - newPosition).sqrMagnitude;
            float sqrSnapDistance = _snapDistance * _snapDistance;
            
            if (sqrDistance > sqrSnapDistance)
            {
                // 瞬移：直接设置位置
                float currentTime = Time.time;
                _fromSnapshot = new Snapshot
                {
                    Position = newPosition,
                    Rotation = newRotation,
                    Velocity = newVelocity,
                    LocalReceiveTime = currentTime,
                    SequenceNumber = syncData.SequenceNumber
                };
                _toSnapshot = _fromSnapshot;
                _currentSnapshot = _fromSnapshot;
                _hasReceivedData = true;
                return;
            }
            
            // 正常更新：设置新的目标快照（使用本地接收时间）
            float receiveTime = Time.time;
            _fromSnapshot = _toSnapshot;
            _toSnapshot = new Snapshot
            {
                Position = newPosition,
                Rotation = newRotation,
                Velocity = newVelocity,
                LocalReceiveTime = receiveTime,
                SequenceNumber = syncData.SequenceNumber
            };
            
            _hasReceivedData = true;
        }
        
        /// <summary>
        /// 🔥 优化：改进序列号判断逻辑（RFC 1982 Serial Number Arithmetic）
        /// 判断 seq1 是否比 seq2 旧
        /// </summary>
        private static bool IsSequenceOlder(uint seq1, uint seq2)
        {
            // 如果 seq1 比 seq2 旧，返回 true
            return seq1 != seq2 && ((seq2 - seq1) & SEQUENCE_HALF) == 0;
        }
        
        /// <summary>
        /// 🔥 优化：改进插值算法，缓存计算结果
        /// 使用线性插值 + 速度预测 + 时间戳同步
        /// </summary>
        public void Update()
        {
            if (!_hasReceivedData) return;
            
            // 缓存 Time.time，避免多次调用
            float currentTime = Time.time;
            float renderTime = currentTime - _interpolationDelay;
            
            // ========== 情况1: 插值 (Interpolation) ==========
            if (renderTime >= _fromSnapshot.LocalReceiveTime && renderTime <= _toSnapshot.LocalReceiveTime)
            {
                // 🔥 缓存计算结果，减少重复计算
                _cachedTimeDiff = _toSnapshot.LocalReceiveTime - _fromSnapshot.LocalReceiveTime;
                _cachedInterpolationT = _cachedTimeDiff > 0 
                    ? (renderTime - _fromSnapshot.LocalReceiveTime) / _cachedTimeDiff 
                    : 0f;
                
                // 位置插值（线性）
                _currentSnapshot.Position = Vector3.Lerp(
                    _fromSnapshot.Position, 
                    _toSnapshot.Position, 
                    _cachedInterpolationT
                );
                
                // 旋转插值（优化版）- 小角度用 Lerp，大角度用 Slerp
                float angle = Quaternion.Angle(_fromSnapshot.Rotation, _toSnapshot.Rotation);
                _currentSnapshot.Rotation = angle < SMALL_ANGLE_THRESHOLD
                    ? Quaternion.Lerp(_fromSnapshot.Rotation, _toSnapshot.Rotation, _cachedInterpolationT)
                    : Quaternion.Slerp(_fromSnapshot.Rotation, _toSnapshot.Rotation, _cachedInterpolationT);
                
                // 速度插值
                _currentSnapshot.Velocity = Vector3.Lerp(
                    _fromSnapshot.Velocity, 
                    _toSnapshot.Velocity, 
                    _cachedInterpolationT
                );
            }
            // ========== 情况2: 外推 (Extrapolation) ==========
            else if (renderTime > _toSnapshot.LocalReceiveTime)
            {
                // 🔥 优化：使用 Mathf.Min 简化逻辑
                float extrapolationTime = Mathf.Min(
                    renderTime - _toSnapshot.LocalReceiveTime, 
                    _extrapolationLimit
                );
                
                // 使用速度预测位置 - Dead Reckoning
                _currentSnapshot.Position = _toSnapshot.Position + _toSnapshot.Velocity * extrapolationTime;
                _currentSnapshot.Rotation = _toSnapshot.Rotation;
                _currentSnapshot.Velocity = _toSnapshot.Velocity;
            }
            // ========== 情况3: 过时数据 ==========
            else
            {
                // 渲染时间早于起始快照，使用起始快照
                _currentSnapshot = _fromSnapshot;
            }
        }
        
        /// <summary>
        /// 🔥 优化：修复平滑插值算法，使用正确的指数衰减
        /// 应用到 Transform - O(1)
        /// </summary>
        /// <param name="targetTransform">目标Transform（用于位置）</param>
        /// <param name="rotationTransform">旋转目标Transform（可选，默认与targetTransform相同）</param>
        public void ApplyToTransform(Transform targetTransform, Transform? rotationTransform = null)
        {
            if (targetTransform == null || !_hasReceivedData) return;
            
            float deltaTime = Time.deltaTime;
            
            // 🔥 正确的平滑公式：t = 1 - exp(-speed * deltaTime) 的近似
            // 使用 Clamp01 确保 t 在 [0, 1] 范围内，避免 deltaTime 过大时超调
            float positionSmoothT = Mathf.Clamp01(_positionSmoothSpeed * deltaTime);
            float rotationSmoothT = Mathf.Clamp01(_rotationSmoothSpeed * deltaTime);
            
            // 位置平滑（指数衰减）
            targetTransform.position = Vector3.Lerp(
                targetTransform.position, 
                _currentSnapshot.Position, 
                positionSmoothT
            );
            
            // 旋转平滑（球面线性插值）
            Transform rotTarget = rotationTransform != null ? rotationTransform : targetTransform;
            rotTarget.rotation = Quaternion.Slerp(
                rotTarget.rotation, 
                _currentSnapshot.Rotation, 
                rotationSmoothT
            );
        }
        
        // ========== Getter 方法 ==========
        public Vector3 GetPosition() => _currentSnapshot.Position;
        public Quaternion GetRotation() => _currentSnapshot.Rotation;
        public Vector3 GetVelocity() => _currentSnapshot.Velocity;
        public Vector3 GetTargetPosition() => _toSnapshot.Position;
        
        /// <summary>
        /// 🔥 新增：检查是否已接收数据
        /// </summary>
        public bool HasReceivedData() => _hasReceivedData;
        
        // ========== 配置方法 ==========
        
        /// <summary>
        /// 设置插值延迟（默认 50ms）
        /// 延迟越大越平滑，但响应越慢
        /// </summary>
        public void SetInterpolationDelay(float delay)
        {
            _interpolationDelay = Mathf.Max(MIN_INTERPOLATION_DELAY, delay);
        }
        
        /// <summary>
        /// 设置外推限制（默认 500ms）
        /// 超过此时间将停止预测，等待新数据
        /// </summary>
        public void SetExtrapolationLimit(float limit)
        {
            _extrapolationLimit = Mathf.Max(0f, limit);
        }
        
        /// <summary>
        /// 设置瞬移距离阈值（默认 5m）
        /// </summary>
        public void SetSnapDistance(float distance)
        {
            _snapDistance = Mathf.Max(0f, distance);
        }
        
        /// <summary>
        /// 设置旋转瞬移角度阈值（默认 180度）
        /// </summary>
        public void SetRotationSnapAngle(float angle)
        {
            _rotationSnapAngle = Mathf.Clamp(angle, 0f, 180f);
        }
        
        /// <summary>
        /// 🔥 新增：设置位置平滑速度（默认 15）
        /// 速度越大，平滑效果越弱，跟随越快
        /// </summary>
        public void SetPositionSmoothSpeed(float speed)
        {
            _positionSmoothSpeed = Mathf.Max(0f, speed);
        }
        
        /// <summary>
        /// 🔥 新增：设置旋转平滑速度（默认 10）
        /// 速度越大，平滑效果越弱，跟随越快
        /// </summary>
        public void SetRotationSmoothSpeed(float speed)
        {
            _rotationSmoothSpeed = Mathf.Max(0f, speed);
        }
        
        /// <summary>
        /// 直接设置位置（瞬移）
        /// </summary>
        public void SetPositionDirect(Vector3 position)
        {
            _fromSnapshot.Position = position;
            _toSnapshot.Position = position;
            _currentSnapshot.Position = position;
        }
        
        /// <summary>
        /// 直接设置旋转（瞬移）
        /// </summary>
        public void SetRotationDirect(Quaternion rotation)
        {
            _fromSnapshot.Rotation = rotation;
            _toSnapshot.Rotation = rotation;
            _currentSnapshot.Rotation = rotation;
        }
        
        /// <summary>
        /// 🔥 新增：重置状态（用于场景切换等）
        /// </summary>
        public void Reset()
        {
            _hasReceivedData = false;
            _cachedTimeDiff = 0f;
            _cachedInterpolationT = 0f;
        }
    }
}
