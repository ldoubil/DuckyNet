<template>
  <div id="app">
    <!-- 顶部导航栏 -->
    <header class="steam-header">
      <div class="header-logo">
        <h1>🦆 DuckyNet 服务器管理</h1>
      </div>
      <div class="header-nav">
        <button 
          v-for="tab in tabs" 
          :key="tab.id"
          @click="currentTab = tab.id"
          :class="['nav-button', { active: currentTab === tab.id }]">
          {{ tab.name }}
        </button>
      </div>
      <div class="header-time">
        <span>{{ serverTime }}</span>
        <span v-if="wsConnected" class="ws-status connected">🟢 实时</span>
        <span v-else class="ws-status disconnected">🔴 离线</span>
      </div>
    </header>

    <!-- 主要内容区 -->
    <main class="steam-main">
      <!-- 总览面板 -->
      <div v-if="currentTab === 'overview'" class="content-panel">
        <h2 class="panel-title">服务器概览</h2>
        <div class="stats-grid">
          <div class="stat-card">
            <div class="stat-icon">👥</div>
            <div class="stat-content">
              <div class="stat-value">{{ overview.onlinePlayers }}</div>
              <div class="stat-label">在线玩家</div>
            </div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">🏠</div>
            <div class="stat-content">
              <div class="stat-value">{{ overview.totalRooms }}</div>
              <div class="stat-label">活跃房间</div>
            </div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">🤖</div>
            <div class="stat-content">
              <div class="stat-value">{{ overview.totalNpcs }}</div>
              <div class="stat-label">NPC数量</div>
            </div>
          </div>
          <div class="stat-card">
            <div class="stat-icon">⏱️</div>
            <div class="stat-content">
              <div class="stat-value">{{ overview.uptime }}</div>
              <div class="stat-label">运行状态</div>
            </div>
          </div>
        </div>
      </div>

      <!-- 房间列表 -->
      <div v-if="currentTab === 'rooms'" class="content-panel">
        <h2 class="panel-title">房间列表</h2>
        <div class="room-list">
          <div v-for="room in rooms" :key="room.roomId" class="room-card" @click="selectRoom(room)">
            <div class="room-header">
              <span class="room-name">{{ room.roomName }}</span>
              <span class="room-id">{{ room.roomId }}</span>
            </div>
            <div class="room-info">
              <span>房主: {{ room.hostSteamId }}</span>
              <span class="room-players">👥 {{ room.currentPlayers }}/{{ room.maxPlayers }}</span>
            </div>
            <div class="room-meta">
              <span v-if="room.requirePassword" class="badge">🔒 需要密码</span>
              <span v-if="room.isFull" class="badge full">已满</span>
              <span class="room-time">创建: {{ formatTime(room.createTime) }}</span>
            </div>
          </div>
          <div v-if="rooms.length === 0" class="empty-state">
            暂无活跃房间
          </div>
        </div>

        <!-- 房间详情面板 -->
        <div v-if="selectedRoom" class="detail-panel">
          <h3>房间详情 - {{ selectedRoom.roomName }}</h3>
          <button @click="selectedRoom = null" class="close-btn">✖</button>
          <div class="detail-content">
            <p><strong>房间ID:</strong> {{ selectedRoom.roomId }}</p>
            <p><strong>描述:</strong> {{ selectedRoom.description || '无' }}</p>
            <p><strong>房主:</strong> {{ selectedRoom.hostSteamId }}</p>
            <h4>房间内玩家:</h4>
            <div v-if="roomPlayers.length > 0" class="player-mini-list">
              <div v-for="player in roomPlayers" :key="player.steamId" class="player-mini-card">
                <span class="player-name">{{ player.steamName }}</span>
                <span class="player-scene">{{ player.sceneName || '未进入场景' }}</span>
              </div>
            </div>
            <div v-else class="empty-state">暂无玩家</div>
          </div>
        </div>
      </div>

      <!-- 玩家列表 -->
      <div v-if="currentTab === 'players'" class="content-panel">
        <h2 class="panel-title">在线玩家列表</h2>
        <div class="player-list">
          <div v-for="player in players" :key="player.steamId" class="player-card">
            <div class="player-avatar">👤</div>
            <div class="player-info">
              <div class="player-name">{{ player.steamName }}</div>
              <div class="player-id">{{ player.steamId }}</div>
            </div>
            <div class="player-status">
              <div v-if="player.roomName" class="player-room">
                🏠 {{ player.roomName }}
              </div>
              <div v-if="player.sceneName" class="player-scene">
                🗺️ {{ player.sceneName }}{{ player.subSceneName ? '/' + player.subSceneName : '' }}
              </div>
              <div v-if="player.position" class="player-position">
                📍 ({{ player.position.x.toFixed(1) }}, {{ player.position.y.toFixed(1) }}, {{ player.position.z.toFixed(1) }})
              </div>
            </div>
          </div>
          <div v-if="players.length === 0" class="empty-state">
            暂无在线玩家
          </div>
        </div>
      </div>

      <!-- 场景列表 -->
      <div v-if="currentTab === 'scenes'" class="content-panel">
        <h2 class="panel-title">场景列表</h2>
        <div class="scene-list">
          <div v-for="scene in scenes" :key="scene.sceneName + scene.subSceneName" 
               class="scene-card" @click="selectScene(scene)">
            <div class="scene-header">
              <span class="scene-name">{{ scene.sceneName }}</span>
              <span v-if="scene.subSceneName" class="scene-sub">/ {{ scene.subSceneName }}</span>
            </div>
            <div class="scene-stats">
              <span>👥 {{ scene.playerCount }} 玩家</span>
              <span>🤖 {{ scene.npcCount }} NPC</span>
            </div>
          </div>
          <div v-if="scenes.length === 0" class="empty-state">
            暂无活跃场景
          </div>
        </div>

        <!-- 场景详情面板 -->
        <div v-if="selectedScene" class="detail-panel">
          <h3>场景详情 - {{ selectedScene.sceneName }}/{{ selectedScene.subSceneName }}</h3>
          <button @click="selectedScene = null; sceneDetail = null" class="close-btn">✖</button>
          <div class="detail-content" v-if="sceneDetail">
            <h4>场景内玩家:</h4>
            <div v-if="sceneDetail.players.length > 0" class="entity-list">
              <div v-for="player in sceneDetail.players" :key="player.steamId" class="entity-card">
                <span class="entity-name">👤 {{ player.steamName }}</span>
                <span v-if="player.position" class="entity-pos">
                  📍 ({{ player.position.x.toFixed(1) }}, {{ player.position.y.toFixed(1) }}, {{ player.position.z.toFixed(1) }})
                </span>
              </div>
            </div>
            
            <h4>场景内NPC:</h4>
            <div v-if="sceneDetail.npcs.length > 0" class="entity-list">
              <div v-for="npc in sceneDetail.npcs" :key="npc.npcId" class="entity-card npc">
                <div class="npc-main">
                  <span class="entity-name">🤖 {{ npc.npcType }}</span>
                  <span class="npc-id">{{ npc.npcId }}</span>
                </div>
                <div class="npc-stats">
                  <span class="npc-health">❤️ {{ npc.maxHealth }}</span>
                  <span class="entity-pos">
                    📍 ({{ npc.position.x.toFixed(1) }}, {{ npc.position.y.toFixed(1) }}, {{ npc.position.z.toFixed(1) }})
                  </span>
                </div>
                <div class="npc-meta">
                  <span class="npc-owner">拥有者: {{ npc.owner }}</span>
                </div>
              </div>
            </div>
            <div v-else class="empty-state">暂无NPC</div>
          </div>
        </div>
      </div>
    </main>

    <!-- 加载提示 -->
    <div v-if="loading" class="loading-overlay">
      <div class="loading-spinner">加载中...</div>
    </div>
  </div>
</template>

<script>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { api, wsManager } from './services/api'

export default {
  name: 'App',
  setup() {
    const currentTab = ref('overview')
    const tabs = ref([
      { id: 'overview', name: '总览' },
      { id: 'rooms', name: '房间' },
      { id: 'players', name: '玩家' },
      { id: 'scenes', name: '场景' }
    ])
    
    const loading = ref(false)
    const serverTime = ref('')
    const wsConnected = ref(false)
    
    const overview = ref({
      onlinePlayers: 0,
      totalRooms: 0,
      totalNpcs: 0,
      uptime: '运行中'
    })
    
    const rooms = ref([])
    const players = ref([])
    const scenes = ref([])
    const selectedRoom = ref(null)
    const roomPlayers = ref([])
    const selectedScene = ref(null)
    const sceneDetail = ref(null)
    
    let refreshInterval = null
    let timeInterval = null
    
    const updateServerTime = () => {
      const now = new Date()
      serverTime.value = now.toLocaleString('zh-CN')
    }
    
    const loadData = async () => {
      if (currentTab.value === 'overview') {
        await loadOverview()
      } else if (currentTab.value === 'rooms') {
        await loadRooms()
      } else if (currentTab.value === 'players') {
        await loadPlayers()
      } else if (currentTab.value === 'scenes') {
        await loadScenes()
      }
    }
    
    const loadOverview = async () => {
      try {
        const data = await api.getOverview()
        overview.value = data
      } catch (error) {
        console.error('加载概览数据失败:', error)
      }
    }
    
    const loadRooms = async () => {
      try {
        rooms.value = await api.getRooms()
      } catch (error) {
        console.error('加载房间列表失败:', error)
      }
    }
    
    const loadPlayers = async () => {
      try {
        players.value = await api.getPlayers()
      } catch (error) {
        console.error('加载玩家列表失败:', error)
      }
    }
    
    const loadScenes = async () => {
      try {
        scenes.value = await api.getScenes()
      } catch (error) {
        console.error('加载场景列表失败:', error)
      }
    }
    
    const selectRoom = async (room) => {
      selectedRoom.value = room
      try {
        const data = await api.getRoomDetail(room.roomId)
        roomPlayers.value = data.players
      } catch (error) {
        console.error('加载房间详情失败:', error)
      }
    }
    
    const selectScene = async (scene) => {
      selectedScene.value = scene
      try {
        sceneDetail.value = await api.getSceneDetail(scene.sceneName, scene.subSceneName)
      } catch (error) {
        console.error('加载场景详情失败:', error)
      }
    }
    
    const formatTime = (timeStr) => {
      const date = new Date(timeStr)
      return date.toLocaleString('zh-CN')
    }
    
    // WebSocket 消息处理
    const handleWsMessage = (data) => {
      if (data.type === 'overview') {
        overview.value = data.data
      } else if (data.type === 'rooms') {
        rooms.value = data.data
      } else if (data.type === 'players') {
        players.value = data.data
      } else if (data.type === 'scenes') {
        scenes.value = data.data
      }
    }
    
    onMounted(() => {
      updateServerTime()
      loadData()
      
      // 连接WebSocket
      wsManager.connect()
      wsManager.on('message', handleWsMessage)
      wsManager.on('connected', () => {
        wsConnected.value = true
      })
      wsManager.on('disconnected', () => {
        wsConnected.value = false
      })
      
      // 定时刷新（作为WebSocket的备份）
      refreshInterval = setInterval(() => {
        if (!wsConnected.value) {
          loadData()
        }
      }, 5000)
      
      // 每秒更新时间
      timeInterval = setInterval(updateServerTime, 1000)
    })
    
    onUnmounted(() => {
      if (refreshInterval) clearInterval(refreshInterval)
      if (timeInterval) clearInterval(timeInterval)
      wsManager.disconnect()
    })
    
    watch(currentTab, () => {
      selectedRoom.value = null
      selectedScene.value = null
      sceneDetail.value = null
      loadData()
    })
    
    return {
      currentTab,
      tabs,
      loading,
      serverTime,
      wsConnected,
      overview,
      rooms,
      players,
      scenes,
      selectedRoom,
      roomPlayers,
      selectedScene,
      sceneDetail,
      selectRoom,
      selectScene,
      formatTime
    }
  }
}
</script>

