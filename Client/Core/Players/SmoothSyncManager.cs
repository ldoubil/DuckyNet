using System;
using UnityEngine;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 高性能平滑同步管理器
    /// 使用预测性插值（Extrapolation）+ 缓冲快照系统
    /// 算法参考：Source Engine 的网络插值实现
    /// 
    /// 重要：时间戳使用本地接收时间，而非传输的序列号
    /// 这样可以避免客户端/服务器时钟不同步的问题
    /// </summary>
    public class SmoothSyncManager
    {
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
        private float _interpolationDelay = 0.05f;  // 插值延迟 (50ms) - 降低延迟
        private float _extrapolationLimit = 0.5f;   // 外推限制 (500ms)
        private float _snapDistance = 5f;           // 瞬移距离阈值
        private float _rotationSnapAngle = 180f;    // 旋转瞬移角度阈值
        private float _positionSmoothSpeed = 15f;   // 位置平滑速度 (越大越快)
        private float _rotationSmoothSpeed = 10f;   // 旋转平滑速度 (越大越快)
        
        // ========== 运行时数据 ==========
        private float _lastReceiveTime;
        private bool _hasReceivedData = false;
        
        /// <summary>
        /// 初始化平滑同步管理器
        /// </summary>
        public SmoothSyncManager(Vector3 initialPosition, Quaternion initialRotation)
        {
            _fromSnapshot = new Snapshot
            {
                Position = initialPosition,
                Rotation = initialRotation,
                Velocity = Vector3.zero,
                LocalReceiveTime = Time.time,
                SequenceNumber = 0
            };
            
            _toSnapshot = _fromSnapshot;
            _currentSnapshot = _fromSnapshot;
            _lastReceiveTime = Time.time;
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
            
            // 检测异常跳跃（传送/场景切换）
            float positionDelta = Vector3.Distance(_toSnapshot.Position, newPosition);
            if (positionDelta > _snapDistance)
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
                _lastReceiveTime = currentTime;
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
            
            _lastReceiveTime = receiveTime;
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
        /// 更新插值 - 高性能版本
        /// 使用线性插值 + 速度预测 + 时间戳同步
        /// </summary>
        public void Update()
        {
            if (!_hasReceivedData) return;
            
            float currentTime = Time.time;
            float renderTime = currentTime - _interpolationDelay;
            
            // ========== 情况1: 插值 (Interpolation) ==========
            // 如果渲染时间在两个快照之间，进行插值
            if (renderTime >= _fromSnapshot.LocalReceiveTime && renderTime <= _toSnapshot.LocalReceiveTime)
            {
                float timeDiff = _toSnapshot.LocalReceiveTime - _fromSnapshot.LocalReceiveTime;
                float t = timeDiff > 0 ? (renderTime - _fromSnapshot.LocalReceiveTime) / timeDiff : 0f;
                
                // 位置插值（线性）- O(1)
                _currentSnapshot.Position = Vector3.Lerp(
                    _fromSnapshot.Position, 
                    _toSnapshot.Position, 
                    t
                );
                
                // 旋转插值（优化版）- 小角度用 Lerp，大角度用 Slerp
                float angle = Quaternion.Angle(_fromSnapshot.Rotation, _toSnapshot.Rotation);
                if (angle < 10f)
                {
                    // 小角度：使用更快的 Lerp + Normalize
                    _currentSnapshot.Rotation = Quaternion.Lerp(
                        _fromSnapshot.Rotation, 
                        _toSnapshot.Rotation, 
                        t
                    );
                }
                else
                {
                    // 大角度：使用精确的 Slerp
                    _currentSnapshot.Rotation = Quaternion.Slerp(
                        _fromSnapshot.Rotation, 
                        _toSnapshot.Rotation, 
                        t
                    );
                }
                
                // 速度插值
                _currentSnapshot.Velocity = Vector3.Lerp(
                    _fromSnapshot.Velocity, 
                    _toSnapshot.Velocity, 
                    t
                );
            }
            // ========== 情况2: 外推 (Extrapolation) ==========
            // 如果超过最新快照时间，使用速度进行预测
            else if (renderTime > _toSnapshot.LocalReceiveTime)
            {
                float extrapolationTime = renderTime - _toSnapshot.LocalReceiveTime;
                
                // 限制外推时间，防止过度偏移
                if (extrapolationTime > _extrapolationLimit)
                {
                    extrapolationTime = _extrapolationLimit;
                }
                
                // 使用速度预测位置 - Dead Reckoning
                _currentSnapshot.Position = _toSnapshot.Position + 
                                           (_toSnapshot.Velocity * extrapolationTime);
                
                // 旋转保持不变（一般不外推旋转）
                _currentSnapshot.Rotation = _toSnapshot.Rotation;
                
                // 速度保持不变
                _currentSnapshot.Velocity = _toSnapshot.Velocity;
            }
            // ========== 情况3: 过时数据 ==========
            else
            {
                // 渲染时间早于起始快照，使用起始快照
                _currentSnapshot = _fromSnapshot;
            }
            
            // 注意：不再设置 Timestamp，因为已用 LocalReceiveTime 替代
        }
        
        /// <summary>
        /// 应用到 Transform - O(1)
        /// 使用指数衰减平滑插值
        /// </summary>
        /// <param name="targetTransform">目标Transform（用于位置）</param>
        /// <param name="rotationTransform">旋转目标Transform（可选，默认与targetTransform相同）</param>
        public void ApplyToTransform(Transform targetTransform, Transform? rotationTransform = null)
        {
            if (targetTransform == null || !_hasReceivedData) return;
            
            float deltaTime = Time.deltaTime;
            
            // 位置平滑（指数衰减）
            targetTransform.position = Vector3.Lerp(
                targetTransform.position, 
                _currentSnapshot.Position, 
                _positionSmoothSpeed * deltaTime
            );
            
            // 旋转平滑（球面线性插值）
            // 🔥 如果指定了旋转目标，使用它；否则使用位置目标
            Transform rotTarget = rotationTransform ?? targetTransform;
            rotTarget.rotation = Quaternion.Slerp(
                rotTarget.rotation, 
                _currentSnapshot.Rotation, 
                _rotationSmoothSpeed * deltaTime
            );
        }
        
        // ========== Getter 方法 ==========
        public Vector3 GetPosition() => _currentSnapshot.Position;
        public Quaternion GetRotation() => _currentSnapshot.Rotation;
        public Vector3 GetVelocity() => _currentSnapshot.Velocity;
        public Vector3 GetTargetPosition() => _toSnapshot.Position;
        
        // ========== 配置方法 ==========
        
        /// <summary>
        /// 设置插值延迟（默认 100ms）
        /// 延迟越大越平滑，但响应越慢
        /// </summary>
        public void SetInterpolationDelay(float delay)
        {
            _interpolationDelay = Mathf.Max(0.05f, delay);
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
    }
}
