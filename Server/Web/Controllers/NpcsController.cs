using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System.Linq;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - NPC管理
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class NpcsController : ControllerBase
    {
        private readonly PlayerNpcManager _npcManager;

        public NpcsController(PlayerNpcManager npcManager)
        {
            _npcManager = npcManager;
        }

        /// <summary>
        /// 获取所有NPC列表
        /// </summary>
        [HttpGet]
        public IActionResult GetAllNpcs()
        {
            var stats = _npcManager.GetStats();
            
            // 获取所有场景中的NPC
            var allNpcs = new System.Collections.Generic.List<object>();
            
            // 这里我们需要遍历所有玩家的NPC
            // 由于PlayerNpcManager没有提供GetAllNpcs方法，我们返回统计信息
            return Ok(new
            {
                totalNpcs = stats.TotalNpcs,
                totalPlayers = stats.TotalPlayers,
                message = "使用 /api/scenes 查看具体场景中的NPC"
            });
        }

        /// <summary>
        /// 获取指定NPC的详细信息
        /// </summary>
        [HttpGet("{npcId}")]
        public IActionResult GetNpcDetail(string npcId)
        {
            var npc = _npcManager.GetNpcById(npcId);
            if (npc == null)
            {
                return NotFound(new { message = "NPC不存在" });
            }

            var owner = _npcManager.GetNpcOwner(npcId);

            return Ok(new
            {
                npcId = npc.NpcId,
                npcType = npc.NpcType,
                sceneName = npc.SceneName,
                subSceneName = npc.SubSceneName,
                position = new
                {
                    x = npc.PositionX,
                    y = npc.PositionY,
                    z = npc.PositionZ
                },
                rotationY = npc.RotationY,
                maxHealth = npc.MaxHealth,
                spawnTime = DateTimeOffset.FromUnixTimeMilliseconds(npc.SpawnTimestamp).UtcDateTime,
                owner
            });
        }
    }
}

