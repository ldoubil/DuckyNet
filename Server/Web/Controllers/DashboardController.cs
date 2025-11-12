using Microsoft.AspNetCore.Mvc;
using DuckyNet.Server.Managers;
using System.Linq;

namespace DuckyNet.Server.Web.Controllers
{
    /// <summary>
    /// 后台管理 - 总览面板
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly PlayerManager _playerManager;
        private readonly RoomManager _roomManager;
        private readonly PlayerNpcManager _npcManager;

        public DashboardController(
            PlayerManager playerManager,
            RoomManager roomManager,
            PlayerNpcManager npcManager)
        {
            _playerManager = playerManager;
            _roomManager = roomManager;
            _npcManager = npcManager;
        }

        /// <summary>
        /// 获取服务器概览信息
        /// </summary>
        [HttpGet("overview")]
        public IActionResult GetOverview()
        {
            var rooms = _roomManager.GetAllRooms();
            var players = _playerManager.GetAllOnlinePlayers();
            var npcStats = _npcManager.GetStats();

            return Ok(new
            {
                onlinePlayers = players.Length,
                totalRooms = rooms.Length,
                totalNpcs = npcStats.TotalNpcs,
                serverTime = DateTime.UtcNow,
                uptime = "运行中" // 可以后续添加真实的运行时间统计
            });
        }
    }
}

