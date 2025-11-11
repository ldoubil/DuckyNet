using UnityEngine;

namespace Milk
{
    /// <summary>
    /// 牛奶粒子发射器 - 负责创建和配置牛奶粒子系统
    /// </summary>
    public class MilkParticleLauncher
    {
        /// <summary>
        /// 发射牛奶粒子特效
        /// </summary>
        /// <param name="characterControl">角色控制器</param>
        public static void ShootMilkParticles(CharacterMainControl characterControl)
        {
            try
            {
                if (characterControl == null) return;

                GameObject characterObject = characterControl.gameObject;
                if (characterObject == null) return;

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
                                       Vector3.up * ParticleConfig.SHOOT_HEIGHT_OFFSET + 
                                       shootDirection * ParticleConfig.SHOOT_FORWARD_OFFSET;

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
        /// <param name="position">发射位置</param>
        /// <param name="direction">发射方向</param>
        private static void CreateMilkParticleSystem(Vector3 position, Vector3 direction)
        {
            // 创建粒子系统游戏对象
            var particleObj = new GameObject("MilkParticleEffect");
            particleObj.transform.position = position;
            particleObj.transform.rotation = Quaternion.LookRotation(direction);

            // 添加粒子系统组件和黏糊糊效果处理器
            var ps = particleObj.AddComponent<ParticleSystem>();
            particleObj.AddComponent<MilkStickyEffectHandler>();
            
            // 配置粒子系统
            ConfigureParticleSystem(ps);

            // 自动销毁粒子系统
            Object.Destroy(particleObj, ParticleConfig.PARTICLE_SYSTEM_LIFETIME);

            // 播放并立即发射粒子
            ps.Play();
            ps.Emit(ParticleConfig.PARTICLE_COUNT);
        }

        /// <summary>
        /// 配置粒子系统的各个模块
        /// </summary>
        /// <param name="ps">粒子系统</param>
        private static void ConfigureParticleSystem(ParticleSystem ps)
        {
            ConfigureMainModule(ps);
            ConfigureEmissionModule(ps);
            ConfigureShapeModule(ps);
            ConfigureRotationModule(ps);
            ConfigureRenderer(ps);
            ConfigureCollisionModule(ps);
            ConfigureTrailsModule(ps);
        }

        /// <summary>
        /// 配置主模块
        /// </summary>
        private static void ConfigureMainModule(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = ParticleConfig.BURST_DURATION;  // 短时间爆发
            main.loop = false;               // 不循环
            main.startLifetime = new ParticleSystem.MinMaxCurve(ParticleConfig.PARTICLE_LIFETIME_MIN, ParticleConfig.PARTICLE_LIFETIME_MAX);
            main.startSpeed = new ParticleSystem.MinMaxCurve(ParticleConfig.SHOOT_FORCE * 0.8f, ParticleConfig.SHOOT_FORCE * 1.2f);  // 速度随机范围
            main.startSize = new ParticleSystem.MinMaxCurve(ParticleConfig.PARTICLE_SIZE_MIN, ParticleConfig.PARTICLE_SIZE_MAX);
            main.startColor = Color.white;  // 牛奶白色
            main.gravityModifier = new ParticleSystem.MinMaxCurve(ParticleConfig.GRAVITY_MODIFIER * 0.8f, ParticleConfig.GRAVITY_MODIFIER * 1.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;  // 世界空间模拟
            main.maxParticles = ParticleConfig.PARTICLE_COUNT * 2;  // 最大粒子数
        }

        /// <summary>
        /// 配置发射模块
        /// </summary>
        private static void ConfigureEmissionModule(ParticleSystem ps)
        {
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            ParticleSystem.Burst burst = new ParticleSystem.Burst(0f, ParticleConfig.PARTICLE_COUNT);
            emission.SetBurst(0, burst);
        }

        /// <summary>
        /// 配置形状模块
        /// </summary>
        private static void ConfigureShapeModule(ParticleSystem ps)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = ParticleConfig.EMISSION_CONE_ANGLE;              // 锥形发射角度
            shape.radius = ParticleConfig.EMISSION_CONE_RADIUS;            // 锥形底面半径
            shape.radiusThickness = ParticleConfig.EMISSION_RADIUS_THICKNESS;  // 从锥形边缘发射
        }

        /// <summary>
        /// 配置旋转模块
        /// </summary>
        private static void ConfigureRotationModule(ParticleSystem ps)
        {
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(ParticleConfig.ROTATION_SPEED_MIN, ParticleConfig.ROTATION_SPEED_MAX);  // 随机旋转速度
        }

        /// <summary>
        /// 配置渲染器
        /// </summary>
        private static void ConfigureRenderer(ParticleSystem ps)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
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
        }

        /// <summary>
        /// 配置碰撞模块
        /// </summary>
        private static void ConfigureCollisionModule(ParticleSystem ps)
        {
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.dampen = ParticleConfig.COLLISION_DAMPEN;                // 阻尼系数
            collision.bounce = ParticleConfig.COLLISION_BOUNCE;                // 反弹系数
            collision.lifetimeLoss = ParticleConfig.COLLISION_LIFETIME_LOSS;   // 碰撞后生命损失
            collision.collidesWith = ~0;                        // 与所有层碰撞
            collision.sendCollisionMessages = true;             // 发送碰撞消息（用于黏糊糊效果）
        }

        /// <summary>
        /// 配置拖尾模块
        /// </summary>
        private static void ConfigureTrailsModule(ParticleSystem ps)
        {
            var trails = ps.trails;
            trails.enabled = true;
            trails.ratio = 1.0f;                            // 所有粒子都有拖尾
            trails.lifetime = ParticleConfig.TRAIL_LIFETIME;               // 拖尾持续时间
            trails.minVertexDistance = ParticleConfig.TRAIL_MIN_VERTEX_DISTANCE;  // 顶点最小距离
            trails.worldSpace = true;                       // 世界空间拖尾
            trails.dieWithParticles = true;                 // 粒子消失时拖尾也消失
            trails.sizeAffectsWidth = true;                 // 粒子大小影响拖尾宽度
            trails.inheritParticleColor = true;             // 继承粒子颜色
        }
    }
}