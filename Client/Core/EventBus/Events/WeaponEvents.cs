using UnityEngine;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 本地玩家开枪事件
    /// 当主角开枪时触发（订阅自 ItemAgent_Gun.OnMainCharacterShootEvent）
    /// </summary>
    public class LocalPlayerShootEvent
    {
        /// <summary>
        /// 枪械对象（ItemAgent_Gun）
        /// </summary>
        public object Gun { get; }
        
        /// <summary>
        /// 枪口位置
        /// </summary>
        public Vector3 MuzzlePosition { get; }
        
        /// <summary>
        /// 射击方向（枪口朝向）
        /// </summary>
        public Vector3 ShootDirection { get; }
        
        /// <summary>
        /// 枪口 Transform
        /// </summary>
        public Transform? Muzzle { get; }

        public LocalPlayerShootEvent(object gun, Vector3 muzzlePosition, Vector3 shootDirection, Transform? muzzle)
        {
            Gun = gun;
            MuzzlePosition = muzzlePosition;
            ShootDirection = shootDirection;
            Muzzle = muzzle;
        }
    }
}

