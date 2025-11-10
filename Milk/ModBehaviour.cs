using System;
using System.Collections.Generic;
using System.Linq;
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
        #region 常量配置

        /// <summary>
        /// 粒子初始发射速度 (m/s)
        /// </summary>
        private const float SHOOT_FORCE = 20f;

        /// <summary>
        /// 重力倍数（1.0 = 正常重力）
        /// </summary>
        private const float GRAVITY_MODIFIER = 1.0f;

        /// <summary>
        /// 每次发射的粒子数量
        /// </summary>
        private const int PARTICLE_COUNT = 150;

        /// <summary>
        /// 发射位置高度偏移（米）- 从角色脚底向上的高度
        /// </summary>
        private const float SHOOT_HEIGHT_OFFSET = 1f;

        /// <summary>
        /// 发射位置前方偏移（米）- 避免被玩家自己挡住
        /// </summary>
        private const float SHOOT_FORWARD_OFFSET = 0.8f;

        /// <summary>
        /// 粒子持续时间 - 最小值（秒）
        /// </summary>
        private const float PARTICLE_LIFETIME_MIN = 1.0f;

        /// <summary>
        /// 粒子持续时间 - 最大值（秒）
        /// </summary>
        private const float PARTICLE_LIFETIME_MAX = 3.0f;

        /// <summary>
        /// 粒子大小 - 最小值（米）
        /// </summary>
        private const float PARTICLE_SIZE_MIN = 0.15f;

        /// <summary>
        /// 粒子大小 - 最大值（米）
        /// </summary>
        private const float PARTICLE_SIZE_MAX = 0.35f;

        /// <summary>
        /// 粒子系统自动销毁时间（秒）
        /// </summary>
        private const float PARTICLE_SYSTEM_LIFETIME = 10f;

        /// <summary>
        /// 粒子发射锥形角度（度）
        /// </summary>
        private const float EMISSION_CONE_ANGLE = 15f;

        /// <summary>
        /// 粒子发射锥形半径（米）
        /// </summary>
        private const float EMISSION_CONE_RADIUS = 0.1f;

        /// <summary>
        /// 粒子发射位置分布（0=中心，1=边缘）
        /// </summary>
        private const float EMISSION_RADIUS_THICKNESS = 0.5f;

        /// <summary>
        /// 粒子旋转速度 - 最小值（度/秒）
        /// </summary>
        private const float ROTATION_SPEED_MIN = -180f;

        /// <summary>
        /// 粒子旋转速度 - 最大值（度/秒）
        /// </summary>
        private const float ROTATION_SPEED_MAX = 180f;

        /// <summary>
        /// 粒子爆发持续时间（秒）- 短时间爆发
        /// </summary>
        private const float BURST_DURATION = 0.2f;

        /// <summary>
        /// 碰撞阻尼系数（0-1）
        /// </summary>
        private const float COLLISION_DAMPEN = 0.5f;

        /// <summary>
        /// 碰撞反弹系数（0-1）
        /// </summary>
        private const float COLLISION_BOUNCE = 0.3f;

        /// <summary>
        /// 碰撞后生命损失系数（0-1）
        /// </summary>
        private const float COLLISION_LIFETIME_LOSS = 0.1f;

        /// <summary>
        /// 拖尾持续时间（秒）
        /// </summary>
        private const float TRAIL_LIFETIME = 0.3f;

        /// <summary>
        /// 拖尾顶点最小距离（米）
        /// </summary>
        private const float TRAIL_MIN_VERTEX_DISTANCE = 0.02f;

        #endregion

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
                if (characterControl == null)
                {
                    return;
                }

                GameObject characterObject = characterControl.gameObject;
                if (characterObject == null)
                {
                    return;
                }

                // 获取玩家当前的瞄准方向
                Vector3 aimDirection = characterControl.CurrentAimDirection;
                if (aimDirection == Vector3.zero)
                {
                    // 如果瞄准方向为零，使用角色前方
                    aimDirection = characterObject.transform.forward;
                }

                // 发射方向：玩家瞄准的方向
                Vector3 shootDirection = aimDirection.normalized;

                // 发射位置：从角色脚底位置 + 高度偏移 + 前方偏移
                Vector3 shootPosition = characterObject.transform.position + 
                                       Vector3.up * SHOOT_HEIGHT_OFFSET + 
                                       shootDirection * SHOOT_FORWARD_OFFSET;

                CreateMilkParticleSystem(shootPosition, shootDirection);
            }
            catch
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 创建牛奶粒子系统
        /// </summary>
        private void CreateMilkParticleSystem(Vector3 position, Vector3 direction)
        {
            // 创建粒子系统游戏对象
            var particleObj = new GameObject("MilkParticleEffect");
            particleObj.transform.position = position;
            particleObj.transform.rotation = Quaternion.LookRotation(direction);

            // 添加粒子系统组件和黏糊糊效果处理器
            var ps = particleObj.AddComponent<ParticleSystem>();
            particleObj.AddComponent<MilkStickyEffectHandler>();
            
            // 配置主模块
            var main = ps.main;
            main.duration = BURST_DURATION;  // 短时间爆发
            main.loop = false;               // 不循环
            main.startLifetime = new ParticleSystem.MinMaxCurve(PARTICLE_LIFETIME_MIN, PARTICLE_LIFETIME_MAX);
            main.startSpeed = new ParticleSystem.MinMaxCurve(SHOOT_FORCE * 0.8f, SHOOT_FORCE * 1.2f);  // 速度随机范围
            main.startSize = new ParticleSystem.MinMaxCurve(PARTICLE_SIZE_MIN, PARTICLE_SIZE_MAX);
            main.startColor = Color.white;  // 牛奶白色
            main.gravityModifier = new ParticleSystem.MinMaxCurve(GRAVITY_MODIFIER * 0.8f, GRAVITY_MODIFIER * 1.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;  // 世界空间模拟
            main.maxParticles = PARTICLE_COUNT * 2;  // 最大粒子数

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, PARTICLE_COUNT);
            emission.SetBurst(0, burst);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = EMISSION_CONE_ANGLE;              // 锥形发射角度
            shape.radius = EMISSION_CONE_RADIUS;            // 锥形底面半径
            shape.radiusThickness = EMISSION_RADIUS_THICKNESS;  // 从锥形边缘发射

            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(ROTATION_SPEED_MIN, ROTATION_SPEED_MAX);  // 随机旋转速度

            // 配置渲染器（白色材质）
            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sortingOrder = 100;
                
                // 尝试找到合适的粒子 Shader
                Shader? particleShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended") 
                                       ?? Shader.Find("Particles/Alpha Blended Premultiply")
                                       ?? Shader.Find("Particles/Alpha Blended")
                                       ?? Shader.Find("Mobile/Particles/Alpha Blended")
                                       ?? Shader.Find("Sprites/Default");
                
                if (particleShader != null)
                {
                    Material milkMaterial = new Material(particleShader);
                    milkMaterial.color = Color.white;
                    
                    // 设置粒子颜色属性（如果存在）
                    if (milkMaterial.HasProperty("_TintColor"))
                    {
                        milkMaterial.SetColor("_TintColor", Color.white);
                    }
                    
                    renderer.material = milkMaterial;
                    renderer.trailMaterial = new Material(milkMaterial);  // 拖尾使用相同材质
                }
            }

            // 配置碰撞模块（与世界碰撞）
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = COLLISION_DAMPEN;                // 阻尼系数
            collision.bounce = COLLISION_BOUNCE;                // 反弹系数
            collision.lifetimeLoss = COLLISION_LIFETIME_LOSS;   // 碰撞后生命损失
            collision.collidesWith = ~0;                        // 与所有层碰撞
            collision.sendCollisionMessages = true;             // 发送碰撞消息（用于黏糊糊效果）

            // 配置拖尾模块（液体拖尾效果）
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 1.0f;                            // 所有粒子都有拖尾
            trails.lifetime = TRAIL_LIFETIME;               // 拖尾持续时间
            trails.minVertexDistance = TRAIL_MIN_VERTEX_DISTANCE;  // 顶点最小距离
            trails.worldSpace = true;                       // 世界空间拖尾
            trails.dieWithParticles = true;                 // 粒子消失时拖尾也消失
            trails.sizeAffectsWidth = true;                 // 粒子大小影响拖尾宽度
            trails.inheritParticleColor = true;             // 继承粒子颜色

            // 自动销毁粒子系统
            GameObject.Destroy(particleObj, PARTICLE_SYSTEM_LIFETIME);

            // 播放并立即发射粒子
            ps.Play();
            ps.Emit(PARTICLE_COUNT);
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

    /// <summary>
    /// 粒子碰撞监听器 - 创建黏糊糊的拉丝效果
    /// </summary>
    public class MilkStickyEffectHandler : MonoBehaviour
    {
        #region 常量配置

        /// <summary>
        /// 最大线条数量（增加到支持更多拉丝）
        /// </summary>
        private const int MAX_STICKY_LINES = 200;

        /// <summary>
        /// 创建连线的最小距离（米）
        /// </summary>
        private const float MIN_CONNECTION_DISTANCE = 0.1f;

        /// <summary>
        /// 创建连线的最大距离（米）
        /// </summary>
        private const float MAX_CONNECTION_DISTANCE = 10f;

        /// <summary>
        /// 创建连线的概率（0-1）- 每次碰撞时尝试连接的概率
        /// </summary>
        private const float LINE_CREATE_CHANCE = 0.9f;

        /// <summary>
        /// 自身粘连概率（0-1）
        /// </summary>
        private const float SELF_STICK_CHANCE = 0.9f;

        /// <summary>
        /// 垂直向上法线阈值 - 超过此值视为地面
        /// </summary>
        private const float VERTICAL_NORMAL_THRESHOLD = 0.8f;

        /// <summary>
        /// 垂直向下法线阈值 - 低于此值视为天花板
        /// </summary>
        private const float CEILING_NORMAL_THRESHOLD = -0.5f;

        /// <summary>
        /// 高度差阈值（米）- 超过此值认为有高度差，可以创建连线
        /// </summary>
        private const float HEIGHT_DIFFERENCE_THRESHOLD = 0.3f;

        /// <summary>
        /// 自身粘连最大距离（米）
        /// </summary>
        private const float SELF_STICK_MAX_DISTANCE = 30f;

        /// <summary>
        /// 最大碰撞点缓存数量
        /// </summary>
        private const int MAX_COLLISION_POINTS = 100;

        /// <summary>
        /// 每次碰撞连接到最近点的数量（每个新点连接多个旧点）
        /// </summary>
        private const int NEARBY_CONNECTIONS = 5;

        /// <summary>
        /// 自身粘连连接数量（同一物体上的连接）
        /// </summary>
        private const int SELF_STICK_CONNECTIONS = 3;

        /// <summary>
        /// 是否启用连线功能（暂时禁用）
        /// </summary>
        private const bool ENABLE_STICKY_LINES = false;

        /// <summary>
        /// 黏糊糊线条存活时间（秒）
        /// </summary>
        private const float STICKY_LINE_LIFETIME = 8f;

        /// <summary>
        /// 线条拉伸断裂距离倍数 - 当拉伸超过原长度此倍数时断裂
        /// </summary>
        private const float LINE_BREAK_DISTANCE_MULTIPLIER = 3f;

        /// <summary>
        /// 线条拉伸变细速度（每秒）
        /// </summary>
        private const float LINE_THINNING_SPEED = 0.05f;

        /// <summary>
        /// 贴花扩散速度（米/秒）
        /// </summary>
        private const float DECAL_SPREAD_SPEED = 0.05f;

        /// <summary>
        /// 贴花最大扩散倍数
        /// </summary>
        private const float DECAL_MAX_SPREAD_MULTIPLIER = 1.5f;

        #endregion

        private List<CollisionPointInfo> _collisionPoints = new List<CollisionPointInfo>();
        private List<GameObject> _lineObjects = new List<GameObject>();
        private ParticleSystem? _ps;
        private List<ParticleCollisionEvent> _collisionEvents = new List<ParticleCollisionEvent>();
        
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
                bool isGround = normal.y > VERTICAL_NORMAL_THRESHOLD;
                
                // 创建贴花
                CreateSplatDecal(collisionPoint, normal);
                
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
                if (ENABLE_STICKY_LINES && _collisionPoints.Count >= 2 && _lineObjects.Count < MAX_STICKY_LINES)
                {
                    // 查找可以连接的点（更宽松的条件）
                    var nearbyPoints = _collisionPoints
                        .Where(p => p != pointInfo && 
                               Vector3.Distance(p.Position, collisionPoint) > MIN_CONNECTION_DISTANCE &&
                               CanCreateConnection(pointInfo, p, collisionPoint))  // 新的连接判断
                        .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                        .Take(NEARBY_CONNECTIONS)
                        .ToList();

                    foreach (var nearPoint in nearbyPoints)
                    {
                        if (_lineObjects.Count >= MAX_STICKY_LINES) break;
                        
                        float distance = Vector3.Distance(nearPoint.Position, collisionPoint);
                        
                        // 直接使用固定概率创建连线
                        if (distance < MAX_CONNECTION_DISTANCE && UnityEngine.Random.value < LINE_CREATE_CHANCE)
                        {
                            CreateStickyLine(nearPoint.Position, collisionPoint);
                        }
                    }
                    
                    // 2. 连接同一物体上的点（自身粘连）
                    var sameObjectPoints = _collisionPoints
                        .Where(p => p.HitObject == other && 
                               p != pointInfo &&
                               CanCreateConnection(pointInfo, p, collisionPoint))
                        .OrderBy(p => Vector3.Distance(p.Position, collisionPoint))
                        .Take(SELF_STICK_CONNECTIONS)
                        .ToList();
                        
                    foreach (var samePoint in sameObjectPoints)
                    {
                        if (_lineObjects.Count >= MAX_STICKY_LINES) break;
                        
                        float distance = Vector3.Distance(samePoint.Position, collisionPoint);
                        
                        // 自身粘连使用固定高概率
                        if (distance > MIN_CONNECTION_DISTANCE && 
                            distance < SELF_STICK_MAX_DISTANCE && 
                            UnityEngine.Random.value < SELF_STICK_CHANCE)
                        {
                            CreateStickyLine(samePoint.Position, collisionPoint);
                        }
                    }
                }

                // 限制碰撞点缓存数量
                if (_collisionPoints.Count > MAX_COLLISION_POINTS)
                {
                    _collisionPoints.RemoveAt(0);
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
            if (heightDiff > HEIGHT_DIFFERENCE_THRESHOLD)
            {
                return true;  // 有高度差，可以连接（例如楼梯、斜坡）
            }
            
            // 3. 两个点都在平整地面上且高度差很小 - 不创建连接
            return false;
        }

        /// <summary>
        /// 创建黏糊糊拉丝线条
        /// </summary>
        private void CreateStickyLine(Vector3 start, Vector3 end)
        {
            const float LINE_START_WIDTH = 0.12f;      // 线条起始宽度
            const float LINE_END_WIDTH = 0.06f;        // 线条结束宽度
            const int LINE_SEGMENTS = 8;               // 线条分段数
            const float SAG_FACTOR = 0.3f;             // 下垂系数（距离的倍数）
            const float WOBBLE_RANGE = 0.03f;          // 摆动范围
            
            GameObject lineObj = new GameObject("MilkStickyLine");
            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

            // 配置线条渲染器
            lineRenderer.startWidth = LINE_START_WIDTH;
            lineRenderer.endWidth = LINE_END_WIDTH;
            lineRenderer.positionCount = LINE_SEGMENTS;
            lineRenderer.useWorldSpace = true;
            
            // 创建自然下垂的曲线
            Vector3[] points = new Vector3[LINE_SEGMENTS];
            float distance = Vector3.Distance(start, end);
            float sagAmount = distance * SAG_FACTOR;
            
            for (int i = 0; i < LINE_SEGMENTS; i++)
            {
                float t = i / (float)(LINE_SEGMENTS - 1);
                Vector3 point = Vector3.Lerp(start, end, t);
                
                // 抛物线下垂（模拟重力）
                float sag = sagAmount * Mathf.Sin(t * Mathf.PI);
                point.y -= sag;
                
                // 添加随机摆动（不规则感）
                point += new Vector3(
                    UnityEngine.Random.Range(-WOBBLE_RANGE, WOBBLE_RANGE),
                    UnityEngine.Random.Range(-WOBBLE_RANGE * 0.67f, WOBBLE_RANGE * 0.67f),
                    UnityEngine.Random.Range(-WOBBLE_RANGE, WOBBLE_RANGE)
                );
                
                points[i] = point;
            }
            
            lineRenderer.SetPositions(points);

            // 设置白色半透明材质
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                lineRenderer.material = new Material(shader);
                lineRenderer.startColor = new Color(1f, 1f, 1f, 0.7f);  // 起始半透明
                lineRenderer.endColor = new Color(1f, 1f, 1f, 0.4f);    // 结束更透明
            }

            // 添加动态下垂效果（包含拉伸断裂和变细）
            var drippingEffect = lineObj.AddComponent<LineDrippingEffect>();
            drippingEffect.Initialize(points, distance, LINE_BREAK_DISTANCE_MULTIPLIER, LINE_THINNING_SPEED);

            // 自动销毁
            Destroy(lineObj, STICKY_LINE_LIFETIME);
            _lineObjects.Add(lineObj);

            // 清理已销毁的线条引用
            _lineObjects.RemoveAll(obj => obj == null);
        }
        
        /// <summary>
        /// 在碰撞点创建圆形贴花
        /// </summary>
        private void CreateSplatDecal(Vector3 position, Vector3 normal)
        {
            const int DECAL_SEGMENTS = 16;          // 圆形分段数
            const float DECAL_OFFSET = 0.01f;       // 贴花表面偏移（避免 Z-fighting）
            const float DECAL_RADIUS_MIN = 0.2f;    // 贴花最小半径
            const float DECAL_RADIUS_MAX = 0.4f;    // 贴花最大半径
            const float DECAL_IRREGULARITY_MIN = 0.8f;  // 边缘不规则最小值
            const float DECAL_IRREGULARITY_MAX = 1.2f;  // 边缘不规则最大值
            const float DECAL_ALPHA = 0.8f;         // 贴花透明度
            const float DECAL_LIFETIME = 6f;        // 贴花存活时间（秒）
            
            GameObject decalObj = new GameObject("MilkSplatDecal");
            decalObj.transform.position = position + normal * DECAL_OFFSET;  // 稍微偏移避免 Z-fighting
            decalObj.transform.rotation = Quaternion.LookRotation(-normal);  // 面向法线反方向
            
            var meshFilter = decalObj.AddComponent<MeshFilter>();
            var meshRenderer = decalObj.AddComponent<MeshRenderer>();
            
            // 创建圆形网格（使用多边形近似）
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[DECAL_SEGMENTS + 1];
            int[] triangles = new int[DECAL_SEGMENTS * 3];
            Vector2[] uvs = new Vector2[DECAL_SEGMENTS + 1];
            
            // 中心点
            vertices[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);
            
            // 随机半径（不规则大小）
            float radius = UnityEngine.Random.Range(DECAL_RADIUS_MIN, DECAL_RADIUS_MAX);
            
            // 生成圆形顶点
            for (int i = 0; i < DECAL_SEGMENTS; i++)
            {
                float angle = (i / (float)DECAL_SEGMENTS) * Mathf.PI * 2f;
                float irregularity = UnityEngine.Random.Range(DECAL_IRREGULARITY_MIN, DECAL_IRREGULARITY_MAX);
                float r = radius * irregularity;
                
                vertices[i + 1] = new Vector3(
                    Mathf.Cos(angle) * r,
                    Mathf.Sin(angle) * r,
                    0
                );
                uvs[i + 1] = new Vector2(
                    Mathf.Cos(angle) * 0.5f + 0.5f,
                    Mathf.Sin(angle) * 0.5f + 0.5f
                );
                
                // 三角形索引
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % DECAL_SEGMENTS + 1;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;
            
            // 创建半透明白色材质
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            if (shader != null)
            {
                var material = new Material(shader);
                material.color = new Color(1f, 1f, 1f, DECAL_ALPHA);
                meshRenderer.material = material;
            }
            
            // 添加渐变消失和扩散效果
            var fadeEffect = decalObj.AddComponent<DecalFadeEffect>();
            fadeEffect.Initialize(DECAL_LIFETIME, DECAL_SPREAD_SPEED, DECAL_MAX_SPREAD_MULTIPLIER);
            
            // 自动销毁
            Destroy(decalObj, DECAL_LIFETIME);
            _lineObjects.Add(decalObj);
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

    /// <summary>
    /// 线条动态下垂、拉伸变细和断裂效果
    /// </summary>
    public class LineDrippingEffect : MonoBehaviour
    {
        /// <summary>
        /// 下垂速度（米/秒）
        /// </summary>
        private const float DRIPPING_SPEED = 0.5f;

        private LineRenderer? _lineRenderer;
        private Vector3[] _originalPoints = Array.Empty<Vector3>();
        private float _time;
        private float _originalDistance;
        private float _breakDistanceMultiplier;
        private float _thinningSpeed;
        private float _initialStartWidth;
        private float _initialEndWidth;

        public void Initialize(Vector3[] points, float originalDistance, float breakMultiplier, float thinningSpeed)
        {
            _originalPoints = new Vector3[points.Length];
            Array.Copy(points, _originalPoints, points.Length);
            _originalDistance = originalDistance;
            _breakDistanceMultiplier = breakMultiplier;
            _thinningSpeed = thinningSpeed;
        }

        private void Start()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer != null)
            {
                _initialStartWidth = _lineRenderer.startWidth;
                _initialEndWidth = _lineRenderer.endWidth;
            }
        }

        private void Update()
        {
            if (_lineRenderer == null || _originalPoints.Length == 0) return;

            _time += Time.deltaTime;
            
            // 计算当前线条的实际长度
            Vector3 startPoint = _originalPoints[0];
            Vector3 endPoint = _originalPoints[_originalPoints.Length - 1];
            float currentDistance = Vector3.Distance(_lineRenderer.GetPosition(0), 
                                                      _lineRenderer.GetPosition(_originalPoints.Length - 1));
            
            // 如果拉伸超过原长度的倍数，线条断裂（销毁）
            if (currentDistance > _originalDistance * _breakDistanceMultiplier)
            {
                Destroy(gameObject);
                return;
            }
            
            // 逐渐下垂（中间部分下垂更多）
            for (int i = 0; i < _originalPoints.Length; i++)
            {
                float t = i / (float)(_originalPoints.Length - 1);
                Vector3 point = _originalPoints[i];
                
                // 使用正弦函数让中间部分下垂更多
                float sagFactor = Mathf.Sin(t * Mathf.PI);
                point.y -= _time * DRIPPING_SPEED * sagFactor;
                
                _lineRenderer.SetPosition(i, point);
            }
            
            // 随时间逐渐变细（拉伸效果）
            float thinningFactor = 1f - (_time * _thinningSpeed);
            thinningFactor = Mathf.Max(0.2f, thinningFactor);  // 最细不低于 20%
            
            _lineRenderer.startWidth = _initialStartWidth * thinningFactor;
            _lineRenderer.endWidth = _initialEndWidth * thinningFactor;
        }
    }

    /// <summary>
    /// 贴花渐变消失和扩散效果
    /// </summary>
    public class DecalFadeEffect : MonoBehaviour
    {
        private MeshRenderer? _renderer;
        private Material? _material;
        private float _duration;
        private float _time;
        private float _initialAlpha;
        private Vector3 _initialScale;
        private float _spreadSpeed;
        private float _maxSpreadMultiplier;

        public void Initialize(float duration, float spreadSpeed, float maxSpreadMultiplier)
        {
            _duration = duration;
            _spreadSpeed = spreadSpeed;
            _maxSpreadMultiplier = maxSpreadMultiplier;
        }

        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
                _initialAlpha = _material != null ? _material.color.a : 1f;
            }
            _initialScale = transform.localScale;
        }

        private void Update()
        {
            if (_material == null) return;

            _time += Time.deltaTime;
            float alpha = _initialAlpha * (1f - _time / _duration);
            
            Color color = _material.color;
            color.a = Mathf.Max(0, alpha);
            _material.color = color;
        }
    }
}

