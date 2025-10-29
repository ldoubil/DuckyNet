using System;
using UnityEngine;
using DuckyNet.Shared.Data;


namespace DuckyNet.Client.Core.Helpers
{
    /// <summary>
    /// 角色同步助手 - 提供 CharacterSyncData 和 Unity 对象之间的转换
    /// </summary>
    public static class CharacterSyncHelper
    {
        /// <summary>
        /// 从 Unity GameObject 创建同步数据
        /// </summary>
        public static CharacterSyncData FromUnity(string playerId, GameObject character)
        {
            var data = new CharacterSyncData
            {
                PlayerId = playerId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (character == null)
            {
                // 在主页/大厅时没有角色是正常的，不需要警告
                return data;
            }

            // 位置和旋转
            var transform = character.transform;
            data.PosX = transform.position.x;
            data.PosY = transform.position.y;
            data.PosZ = transform.position.z;
            data.RotationY = transform.eulerAngles.y;

            // 速度（如果有 Rigidbody）
            var rb = character.GetComponent<Rigidbody>();
            if (rb != null)
            {
                data.VelocityX = rb.velocity.x;
                data.VelocityY = rb.velocity.y;
                data.VelocityZ = rb.velocity.z;
            }

            // 动画参数
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                try
                {
                    data.MoveSpeed = animator.GetFloat("MoveSpeed");
                    data.MoveDirX = animator.GetFloat("MoveDirX");
                    data.MoveDirY = animator.GetFloat("MoveDirY");
                    data.IsDashing = animator.GetBool("Dashing");
                    data.IsDead = animator.GetBool("Die");
                    data.HandState = animator.GetInteger("HandState");
                    data.IsReloading = animator.GetBool("Reloading");
                    data.AttackIndex = animator.GetInteger("AttackIndex");
                }
                catch (Exception ex)
                {
                    // 某些参数可能不存在，忽略
                    Debug.LogWarning($"[CharacterSyncHelper] 读取动画参数失败: {ex.Message}");
                }
            }

            return data;
        }

        /// <summary>
        /// 应用同步数据到 Unity GameObject
        /// </summary>
        public static void ApplyToUnity(this CharacterSyncData data, GameObject character, bool interpolate = true)
        {
            if (character == null)
            {
                Debug.LogWarning($"[CharacterSyncHelper] 角色对象为 null: {data.PlayerId}");
                return;
            }

            var transform = character.transform;

            // 应用位置和旋转
            var targetPos = new Vector3(data.PosX, data.PosY, data.PosZ);
            var targetRot = Quaternion.Euler(0, data.RotationY, 0);

            if (interpolate)
            {
                // 平滑插值
                transform.position = Vector3.Lerp(transform.position, targetPos, 0.3f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.3f);
            }
            else
            {
                // 直接设置
                transform.position = targetPos;
                transform.rotation = targetRot;
            }

            // 应用动画参数
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                try
                {
                    animator.SetFloat("MoveSpeed", data.MoveSpeed);
                    animator.SetFloat("MoveDirX", data.MoveDirX);
                    animator.SetFloat("MoveDirY", data.MoveDirY);
                    animator.SetBool("Dashing", data.IsDashing);
                    animator.SetBool("Die", data.IsDead);
                    animator.SetInteger("HandState", data.HandState);
                    animator.SetBool("Reloading", data.IsReloading);
                    animator.SetInteger("AttackIndex", data.AttackIndex);
                    
                    // 注意：Attack Trigger 需要在调用代码中检测变化后触发
                }
                catch (Exception ex)
                {
                    // 某些参数可能不存在，忽略
                    Debug.LogWarning($"[CharacterSyncHelper] 设置动画参数失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取位置向量
        /// </summary>
        public static Vector3 GetPosition(this CharacterSyncData data)
        {
            return new Vector3(data.PosX, data.PosY, data.PosZ);
        }

        /// <summary>
        /// 获取速度向量
        /// </summary>
        public static Vector3 GetVelocity(this CharacterSyncData data)
        {
            return new Vector3(data.VelocityX, data.VelocityY, data.VelocityZ);
        }

        /// <summary>
        /// 获取旋转
        /// </summary>
        public static Quaternion GetRotation(this CharacterSyncData data)
        {
            return Quaternion.Euler(0, data.RotationY, 0);
        }
    }
}

