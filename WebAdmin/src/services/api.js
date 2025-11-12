import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000'
const WS_BASE_URL = import.meta.env.VITE_WS_BASE_URL || 'ws://localhost:5000'

// 创建axios实例
const http = axios.create({
  baseURL: API_BASE_URL,
  timeout: 10000
})

// 请求拦截器
http.interceptors.request.use(
  config => {
    return config
  },
  error => {
    return Promise.reject(error)
  }
)

// 响应拦截器
http.interceptors.response.use(
  response => {
    return response.data
  },
  error => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)

// API方法
export const api = {
  // 总览
  getOverview: () => http.get('/api/dashboard/overview'),
  
  // 房间
  getRooms: () => http.get('/api/rooms'),
  getRoomDetail: (roomId) => http.get(`/api/rooms/${roomId}`),
  
  // 玩家
  getPlayers: () => http.get('/api/players'),
  getPlayerDetail: (steamId) => http.get(`/api/players/${steamId}`),
  
  // 场景
  getScenes: () => http.get('/api/scenes'),
  getSceneDetail: (sceneName, subSceneName) => http.get(`/api/scenes/${sceneName}/${subSceneName}`),
  
  // NPC
  getNpcs: () => http.get('/api/npcs'),
  getNpcDetail: (npcId) => http.get(`/api/npcs/${npcId}`),
  
  // 监控
  getPerformance: () => http.get('/api/monitor/performance'),
  getPlayerDistribution: () => http.get('/api/monitor/player-distribution'),
  getNpcStats: () => http.get('/api/monitor/npc-stats'),
  getHotScenes: () => http.get('/api/monitor/hot-scenes'),
  getHealth: () => http.get('/api/monitor/health')
}

