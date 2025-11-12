<template>
  <v-app>
    <!-- 顶部导航栏 -->
    <v-app-bar color="primary" dark prominent>
      <v-app-bar-nav-icon @click="drawer = !drawer"></v-app-bar-nav-icon>
      <v-toolbar-title>
        <v-icon left>mdi-duck</v-icon>
        DuckyNet 服务器管理
      </v-toolbar-title>
      
      <v-spacer></v-spacer>
      
      <!-- 自动刷新状态 -->
      <v-chip color="success" label>
        <v-icon left>mdi-refresh</v-icon>
        实时刷新 (0.1秒)
      </v-chip>
      
      <v-btn icon @click="refreshData">
        <v-icon>mdi-refresh</v-icon>
      </v-btn>
      
      <span class="text-caption ml-4">{{ serverTime }}</span>
    </v-app-bar>

    <!-- 侧边导航抽屉 -->
    <v-navigation-drawer v-model="drawer" app>
      <v-list>
        <v-list-item prepend-icon="mdi-view-dashboard" title="总览" value="overview" @click="currentView = 'overview'"></v-list-item>
        <v-list-item prepend-icon="mdi-door" title="房间管理" value="rooms" @click="currentView = 'rooms'"></v-list-item>
        <v-list-item prepend-icon="mdi-account-multiple" title="玩家列表" value="players" @click="currentView = 'players'"></v-list-item>
        <v-list-item prepend-icon="mdi-map" title="场景监控" value="scenes" @click="currentView = 'scenes'"></v-list-item>
        <v-list-item prepend-icon="mdi-robot" title="NPC管理" value="npcs" @click="currentView = 'npcs'"></v-list-item>
        <v-list-item prepend-icon="mdi-chart-line" title="性能监控" value="performance" @click="currentView = 'performance'"></v-list-item>
      </v-list>
    </v-navigation-drawer>

    <!-- 主内容区 -->
    <v-main>
      <v-container fluid>
        <!-- 总览视图 -->
        <div v-if="currentView === 'overview'">
          <v-row>
            <v-col cols="12" md="3">
              <v-card>
                <v-card-text>
                  <div class="text-h4 text-primary">{{ overview.onlinePlayers }}</div>
                  <div class="text-subtitle-1">在线玩家</div>
                  <v-icon size="48" color="primary">mdi-account-multiple</v-icon>
                </v-card-text>
              </v-card>
            </v-col>
            <v-col cols="12" md="3">
              <v-card>
                <v-card-text>
                  <div class="text-h4 text-success">{{ overview.totalRooms }}</div>
                  <div class="text-subtitle-1">活跃房间</div>
                  <v-icon size="48" color="success">mdi-door</v-icon>
                </v-card-text>
              </v-card>
            </v-col>
            <v-col cols="12" md="3">
              <v-card>
                <v-card-text>
                  <div class="text-h4 text-warning">{{ overview.totalNpcs }}</div>
                  <div class="text-subtitle-1">NPC数量</div>
                  <v-icon size="48" color="warning">mdi-robot</v-icon>
                </v-card-text>
              </v-card>
            </v-col>
            <v-col cols="12" md="3">
              <v-card>
                <v-card-text>
                  <div class="text-h4 text-info">{{ overview.uptime }}</div>
                  <div class="text-subtitle-1">运行状态</div>
                  <v-icon size="48" color="info">mdi-check-circle</v-icon>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </div>

        <!-- 房间管理视图 -->
        <div v-if="currentView === 'rooms'">
          <v-row>
            <v-col cols="12" md="8">
              <v-card>
                <v-card-title>房间列表</v-card-title>
                <v-card-text>
                  <v-data-table
                    :headers="roomHeaders"
                    :items="rooms"
                    :items-per-page="10"
                    @click:row="selectRoom">
                    <template v-slot:item.requirePassword="{ item }">
                      <v-icon v-if="item.requirePassword" color="warning">mdi-lock</v-icon>
                      <v-icon v-else color="success">mdi-lock-open</v-icon>
                    </template>
                    <template v-slot:item.isFull="{ item }">
                      <v-chip :color="item.isFull ? 'error' : 'success'" small>
                        {{ item.isFull ? '已满' : '可加入' }}
                      </v-chip>
                    </template>
                    <template v-slot:item.currentPlayers="{ item }">
                      {{ item.currentPlayers }}/{{ item.maxPlayers }}
                    </template>
                  </v-data-table>
                </v-card-text>
              </v-card>
            </v-col>
            
            <v-col cols="12" md="4" v-if="selectedRoom">
              <v-card>
                <v-card-title>房间详情</v-card-title>
                <v-card-text>
                  <v-list>
                    <v-list-item>
                      <v-list-item-title>房间名称</v-list-item-title>
                      <v-list-item-subtitle>{{ selectedRoom.roomName }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <v-list-item-title>房主</v-list-item-title>
                      <v-list-item-subtitle>{{ selectedRoom.hostSteamId }}</v-list-item-subtitle>
                    </v-list-item>
                    <v-list-item>
                      <v-list-item-title>创建时间</v-list-item-title>
                      <v-list-item-subtitle>{{ formatTime(selectedRoom.createTime) }}</v-list-item-subtitle>
                    </v-list-item>
                  </v-list>
                  
                  <v-divider class="my-3"></v-divider>
                  
                  <div class="text-subtitle-1 mb-2">房间内玩家</div>
                  <v-chip v-for="player in roomPlayers" :key="player.steamId" class="ma-1">
                    {{ player.steamName }}
                  </v-chip>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </div>

        <!-- 玩家列表视图 -->
        <div v-if="currentView === 'players'">
          <v-card>
            <v-card-title>在线玩家列表</v-card-title>
            <v-card-text>
              <v-data-table
                :headers="playerHeaders"
                :items="players"
                :items-per-page="15">
                <template v-slot:item.position="{ item }">
                  <span v-if="item.position">
                    ({{ item.position.x.toFixed(1) }}, {{ item.position.y.toFixed(1) }}, {{ item.position.z.toFixed(1) }})
                  </span>
                  <span v-else class="text-grey">-</span>
                </template>
                <template v-slot:item.sceneName="{ item }">
                  {{ item.sceneName || '-' }}
                  <span v-if="item.subSceneName" class="text-grey">/ {{ item.subSceneName }}</span>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </div>

        <!-- 场景监控视图 -->
        <div v-if="currentView === 'scenes'">
          <v-row>
            <v-col cols="12" md="4">
              <v-card>
                <v-card-title>场景列表</v-card-title>
                <v-card-text>
                  <v-list>
                    <v-list-item
                      v-for="scene in scenes"
                      :key="scene.sceneName + scene.subSceneName"
                      @click="selectScene(scene)"
                      :active="selectedScene?.sceneName === scene.sceneName">
                      <v-list-item-title>{{ scene.sceneName }}</v-list-item-title>
                      <v-list-item-subtitle>
                        {{ scene.subSceneName || '主场景' }} - 
                        👥 {{ scene.playerCount }} 玩家 | 
                        🤖 {{ scene.npcCount }} NPC
                      </v-list-item-subtitle>
                    </v-list-item>
                  </v-list>
                </v-card-text>
              </v-card>
            </v-col>
            
            <v-col cols="12" md="8" v-if="selectedScene && sceneDetail">
              <v-card>
                <v-card-title>
                  {{ selectedScene.sceneName }} / {{ selectedScene.subSceneName }}
                </v-card-title>
                <v-card-text>
                  <SceneMap 
                    :players="sceneDetail.players" 
                    :npcs="sceneDetail.npcs" />
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </div>

        <!-- NPC管理视图 -->
        <div v-if="currentView === 'npcs'">
          <v-card>
            <v-card-title>NPC列表</v-card-title>
            <v-card-text>
              <v-data-table
                :headers="npcHeaders"
                :items="allNpcs"
                :items-per-page="20">
                <template v-slot:item.position="{ item }">
                  ({{ item.position.x.toFixed(1) }}, {{ item.position.y.toFixed(1) }}, {{ item.position.z.toFixed(1) }})
                </template>
                <template v-slot:item.maxHealth="{ item }">
                  <v-progress-linear
                    :model-value="100"
                    color="success"
                    height="20">
                    {{ item.maxHealth }}
                  </v-progress-linear>
                </template>
              </v-data-table>
            </v-card-text>
          </v-card>
        </div>

        <!-- 性能监控视图 -->
        <div v-if="currentView === 'performance'">
          <v-row>
            <v-col cols="12">
              <v-card>
                <v-card-title>实时性能监控</v-card-title>
                <v-card-text>
                  <div class="text-h6">服务器统计</div>
                  <v-table>
                    <tbody>
                      <tr>
                        <td>刷新方式</td>
                        <td>HTTP 实时轮询</td>
                      </tr>
                      <tr>
                        <td>更新频率</td>
                        <td>每0.1秒 (10次/秒)</td>
                      </tr>
                      <tr>
                        <td>服务器时间</td>
                        <td>{{ serverTime }}</td>
                      </tr>
                      <tr>
                        <td>在线玩家</td>
                        <td>{{ overview.onlinePlayers }}</td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>
        </div>
      </v-container>
    </v-main>
  </v-app>
</template>

<script>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { api } from './services/api'
import SceneMap from './components/SceneMap.vue'

export default {
  name: 'App',
  components: {
    SceneMap
  },
  setup() {
    const drawer = ref(true)
    const currentView = ref('overview')
    const wsConnected = ref(true) // HTTP轮询模式，始终显示为已连接
    const serverTime = ref('')
    
    const overview = ref({
      onlinePlayers: 0,
      totalRooms: 0,
      totalNpcs: 0,
      uptime: '运行中'
    })
    
    const rooms = ref([])
    const players = ref([])
    const scenes = ref([])
    const allNpcs = ref([])
    
    const selectedRoom = ref(null)
    const roomPlayers = ref([])
    const selectedScene = ref(null)
    const sceneDetail = ref(null)
    
    const roomHeaders = [
      { title: '房间名称', key: 'roomName' },
      { title: '房间ID', key: 'roomId' },
      { title: '房主', key: 'hostSteamId' },
      { title: '玩家', key: 'currentPlayers' },
      { title: '密码', key: 'requirePassword' },
      { title: '状态', key: 'isFull' }
    ]
    
    const playerHeaders = [
      { title: '玩家名称', key: 'steamName' },
      { title: 'Steam ID', key: 'steamId' },
      { title: '所在房间', key: 'roomName' },
      { title: '场景', key: 'sceneName' },
      { title: '位置', key: 'position' }
    ]
    
    const npcHeaders = [
      { title: 'NPC ID', key: 'npcId' },
      { title: 'NPC类型', key: 'npcType' },
      { title: '场景', key: 'sceneName' },
      { title: '位置', key: 'position' },
      { title: '血量', key: 'maxHealth' },
      { title: '拥有者', key: 'owner' }
    ]
    
    let refreshInterval = null
    let timeInterval = null
    
    const updateServerTime = () => {
      serverTime.value = new Date().toLocaleString('zh-CN')
    }
    
    const refreshData = async () => {
      if (currentView.value === 'overview') {
        await loadOverview()
      } else if (currentView.value === 'rooms') {
        await loadRooms()
        // 如果有选中的房间，刷新房间详情
        if (selectedRoom.value) {
          try {
            const data = await api.getRoomDetail(selectedRoom.value.roomId)
            roomPlayers.value = data.players
          } catch (error) {
            // 静默失败
          }
        }
      } else if (currentView.value === 'players') {
        await loadPlayers()
      } else if (currentView.value === 'scenes') {
        await loadScenes()
      }
    }
    
    const loadOverview = async () => {
      try {
        overview.value = await api.getOverview()
      } catch (error) {
        console.error('加载概览失败:', error)
      }
    }
    
    const loadRooms = async () => {
      try {
        rooms.value = await api.getRooms()
      } catch (error) {
        console.error('加载房间失败:', error)
      }
    }
    
    const loadPlayers = async () => {
      try {
        players.value = await api.getPlayers()
      } catch (error) {
        console.error('加载玩家失败:', error)
      }
    }
    
    const loadScenes = async () => {
      try {
        scenes.value = await api.getScenes()
        
        // 如果有选中的场景，实时刷新它的详情
        if (selectedScene.value) {
          try {
            sceneDetail.value = await api.getSceneDetail(
              selectedScene.value.sceneName, 
              selectedScene.value.subSceneName
            )
          } catch (error) {
            console.error('刷新场景详情失败:', error)
          }
        }
        
        // 加载所有场景的NPC
        const allNpcList = []
        for (const scene of scenes.value) {
          const detail = await api.getSceneDetail(scene.sceneName, scene.subSceneName)
          allNpcList.push(...detail.npcs)
        }
        allNpcs.value = allNpcList
      } catch (error) {
        console.error('加载场景失败:', error)
      }
    }
    
    const selectRoom = async (event, { item }) => {
      selectedRoom.value = item
      try {
        const data = await api.getRoomDetail(item.roomId)
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
      return new Date(timeStr).toLocaleString('zh-CN')
    }
    
    onMounted(() => {
      updateServerTime()
      refreshData()
      
      // 每0.1秒自动刷新（实时轮询）
      refreshInterval = setInterval(() => {
        refreshData()
      }, 100)
      
      // 每秒更新时间
      timeInterval = setInterval(updateServerTime, 1000)
      
    })
    
    onUnmounted(() => {
      if (refreshInterval) clearInterval(refreshInterval)
      if (timeInterval) clearInterval(timeInterval)
    })
    
    return {
      drawer,
      currentView,
      wsConnected,
      serverTime,
      overview,
      rooms,
      players,
      scenes,
      allNpcs,
      selectedRoom,
      roomPlayers,
      selectedScene,
      sceneDetail,
      roomHeaders,
      playerHeaders,
      npcHeaders,
      refreshData,
      selectRoom,
      selectScene,
      formatTime
    }
  }
}
</script>
