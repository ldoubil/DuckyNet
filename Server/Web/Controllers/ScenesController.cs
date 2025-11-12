using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System.Linq;
using System.Collections.Generic;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - 场景管理
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ScenesController : ControllerBase
    {
        private readonly PlayerManager _playerManager;
        private readonly SceneManager _sceneManager;
        private readonly PlayerNpcManager _npcManager;

        public ScenesController(
            PlayerManager playerManager,
            SceneManager sceneManager,
            PlayerNpcManager npcManager)
        {
            _playerManager = playerManager;
            _sceneManager = sceneManager;
            _npcManager = npcManager;
        }

        /// <summary>
        /// 获取所有场景列表（根据在线玩家统计）
        /// </summary>
        [HttpGet]
        public IActionResult GetAllScenes()
        {
            var players = _playerManager.GetAllOnlinePlayers();
            
            // 按场景分组统计
            var sceneGroups = players
                .Where(p => !string.IsNullOrEmpty(p.CurrentScenelData?.SceneName))
                .GroupBy(p => new
                {
                    SceneName = p.CurrentScenelData!.SceneName,
                    SubSceneName = p.CurrentScenelData.SubSceneName
                })
                .Select(g => new
                {
                    sceneName = g.Key.SceneName,
                    subSceneName = g.Key.SubSceneName,
                    playerCount = g.Count(),
                    npcCount = _npcManager.GetSceneNpcs(g.Key.SceneName, g.Key.SubSceneName).Count
                })
                .ToList();

            return Ok(sceneGroups);
        }

        /// <summary>
        /// 获取指定场景的详细信息
        /// </summary>
        [HttpGet("{sceneName}/{subSceneName}")]
        public IActionResult GetSceneDetail(string sceneName, string subSceneName)
        {
            var players = _playerManager.GetAllOnlinePlayers()
                .Where(p => p.CurrentScenelData?.SceneName == sceneName &&
                           p.CurrentScenelData?.SubSceneName == subSceneName)
                .ToList();

            var npcs = _npcManager.GetSceneNpcs(sceneName, subSceneName);

            var playerList = players.Select(p =>
            {
                var position = _sceneManager.GetPlayerPosition(p.SteamId);
                return new
                {
                    steamId = p.SteamId,
                    steamName = p.SteamName,
                    position = position != null ? new
                    {
                        x = position.Value.X,
                        y = position.Value.Y,
                        z = position.Value.Z
                    } : null
                };
            }).ToList();

            var npcList = npcs.Select(npc => new
            {
                npcId = npc.NpcId,
                npcType = npc.NpcType,
                position = new
                {
                    x = npc.PositionX,
                    y = npc.PositionY,
                    z = npc.PositionZ
                },
                rotationY = npc.RotationY,
                maxHealth = npc.MaxHealth,
                spawnTime = DateTimeOffset.FromUnixTimeMilliseconds(npc.SpawnTimestamp).UtcDateTime,
                owner = _npcManager.GetNpcOwner(npc.NpcId)
            }).ToList();

            return Ok(new
            {
                sceneName,
                subSceneName,
                players = playerList,
                npcs = npcList
            });
        }
    }
}

