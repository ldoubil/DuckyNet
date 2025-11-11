using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 粒子碰撞监听器 - 创建黏糊糊的拉丝效果
    /// </summary>
    public class MilkStickyEffectHandler : MonoBehaviour
    {
        private List<CollisionPointInfo> _collisionPoints = new List<CollisionPointInfo>();
        private List<GameObject> _lineObjects = new List<GameObject>();
        private ParticleSystem? _ps;
        private List<ParticleCollisionEvent> _collisionEvents = new List<ParticleCollisionEvent>();
        
        /// <summary>
        /// 碰撞点信息
        /// </summary>
        private class CollisionPointInfo
        {
            public Vector3 Position;
            public GameObject? HitObject;
            public float Time;
            public Vector3 Normal;          // 碰撞表面法线
            public bool IsGround;           // 是否为地面（平整的水平向上表面）
        }

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (_ps == null) return;

            int numCollisionEvents = _ps.GetCollisionEvents(other, _collisionEvents);

            for (int i = 0; i < numCollisionEvents; i++)
            {
                Vector3 collisionPoint = _collisionEvents[i].intersection;
                Vector3 normal = _collisionEvents[i].normal;
                
                // 检查是否为平整的地面（水平向上）
                bool isGround = normal.y > ParticleConfig.VERTICAL_NORMAL_THRESHOLD;
                
                // 创建贴花
                SplatDecalCreator.CreateSplatDecal(collisionPoint, normal);
                
                // 添加碰撞点信息
                var pointInfo = new CollisionPointInfo
                {
                    Position = collisionPoint,
                    HitObject = other,
                    Time = Time.time,
                    Normal = normal,
                    IsGround = isGround
                };
                _collisionPoints.Add(pointInfo);

                // 创建黏糊糊连线的条件检查（可通过常量开关）
                if (ParticleConfig.ENABLE_STICKY_LINES && _collisionPoints.Count >= 2 && _lineObjects.Count < ParticleConfig.MAX_STICKY_LINES)
                {
                    CreateStickyConnections(pointInfo, collisionPoint, other);
                }

                // 限制碰撞点缓存数量
                if (_collisionPoints.Count > ParticleConfig.MAX_COLLISION_POINTS)
                {
                    _collisionPoints.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// 创建黏糊糊连接
        /// </summary>
        private void CreateStickyConnections(CollisionPointInfo pointInfo, Vector3 collisionPoint, GameObject other)
        {
            // 1. 查找可以连接的附近点
            var nearbyPoints = _collisionPoints
                .Where(p => p != pointInfo && 
                           Vector3.Distance(p.Position, collisionPoint) > ParticleConfig.MIN_CONNECTION_DISTANCE &&
                           CanCreateConnection(pointInfo, p, collisionPoint))
                .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                .Take(ParticleConfig.NEARBY_CONNECTIONS)
                .ToList();

            foreach (var nearPoint in nearbyPoints)
            {
                if (_lineObjects.Count >= ParticleConfig.MAX_STICKY_LINES) break;
                
                float distance = Vector3.Distance(nearPoint.Position, collisionPoint);
                
                // 直接使用固定概率创建连线
                if (distance < ParticleConfig.MAX_CONNECTION_DISTANCE && UnityEngine.Random.value < ParticleConfig.LINE_CREATE_CHANCE)
                {
                    StickyLineCreator.CreateStickyLine(nearPoint.Position, collisionPoint, _lineObjects);
                }
            }
            
            // 2. 连接同一物体上的点（自身粘连）
            var sameObjectPoints = _collisionPoints
                .Where(p => p.HitObject == other && 
                           p != pointInfo &&
                           CanCreateConnection(pointInfo, p, collisionPoint))
                .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                .Take(ParticleConfig.SELF_STICK_CONNECTIONS)
                .ToList();
                
            foreach (var samePoint in sameObjectPoints)
            {
                if (_lineObjects.Count >= ParticleConfig.MAX_STICKY_LINES) break;
                
                float distance = Vector3.Distance(samePoint.Position, collisionPoint);
                
                // 自身粘连使用固定高概率
                if (distance > ParticleConfig.MIN_CONNECTION_DISTANCE && 
                    distance < ParticleConfig.SELF_STICK_MAX_DISTANCE && 
                    UnityEngine.Random.value < ParticleConfig.SELF_STICK_CHANCE)
                {
                    StickyLineCreator.CreateStickyLine(samePoint.Position, collisionPoint, _lineObjects);
                }
            }
        }

        /// <summary>
        /// 判断两个点之间是否可以创建连接
        /// 规则：
        /// 1. 墙壁上的点 - 可以连接
        /// 2. 天花板上的点 - 可以连接
        /// 3. 有高度差的点 - 可以连接
        /// 4. 两个都在平整地面且高度差小 - 不连接
        /// </summary>
        private bool CanCreateConnection(CollisionPointInfo pointA, CollisionPointInfo pointB, Vector3 currentPosition)
        {
            // 1. 如果至少有一个点不在地面上（墙壁、天花板），可以连接
            if (!pointA.IsGround || !pointB.IsGround)
            {
                return true;
            }
            
            // 2. 如果两点都在地面上，检查高度差
            float heightDiff = Mathf.Abs(pointA.Position.y - pointB.Position.y);
            if (heightDiff > ParticleConfig.HEIGHT_DIFFERENCE_THRESHOLD)
            {
                return true;  // 有高度差，可以连接（例如楼梯、斜坡）
            }
            
            // 3. 两个点都在平整地面上且高度差很小 - 不创建连接
            return false;
        }

        private void OnDestroy()
        {
            foreach (var lineObj in _lineObjects)
            {
                if (lineObj != null)
                {
                    Destroy(lineObj);
                }
            }
            _lineObjects.Clear();
        }
    }
}