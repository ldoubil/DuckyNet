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
  getNpcDetail: (npcId) => http.get(`/api/npcs/${npcId}`)
}

// WebSocket管理器
class WebSocketManager {
  constructor() {
    this.ws = null
    this.reconnectTimer = null
    this.reconnectDelay = 3000
    this.listeners = {
      message: [],
      connected: [],
      disconnected: [],
      error: []
    }
  }
  
  connect() {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      return
    }
    
    try {
      this.ws = new WebSocket(`${WS_BASE_URL}/ws`)
      
      this.ws.onopen = () => {
        console.log('WebSocket 连接成功')
        this.trigger('connected')
        if (this.reconnectTimer) {
          clearTimeout(this.reconnectTimer)
          this.reconnectTimer = null
        }
      }
      
      this.ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data)
          this.trigger('message', data)
        } catch (error) {
          console.error('WebSocket 消息解析失败:', error)
        }
      }
      
      this.ws.onerror = (error) => {
        console.error('WebSocket 错误:', error)
        this.trigger('error', error)
      }
      
      this.ws.onclose = () => {
        console.log('WebSocket 连接关闭')
        this.trigger('disconnected')
        this.reconnect()
      }
    } catch (error) {
      console.error('WebSocket 连接失败:', error)
      this.reconnect()
    }
  }
  
  disconnect() {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer)
      this.reconnectTimer = null
    }
    if (this.ws) {
      this.ws.close()
      this.ws = null
    }
  }
  
  reconnect() {
    if (this.reconnectTimer) {
      return
    }
    console.log(`将在 ${this.reconnectDelay / 1000} 秒后重连...`)
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null
      this.connect()
    }, this.reconnectDelay)
  }
  
  send(data) {
    if (this.ws && this.ws.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(data))
    }
  }
  
  on(event, callback) {
    if (this.listeners[event]) {
      this.listeners[event].push(callback)
    }
  }
  
  off(event, callback) {
    if (this.listeners[event]) {
      const index = this.listeners[event].indexOf(callback)
      if (index > -1) {
        this.listeners[event].splice(index, 1)
      }
    }
  }
  
  trigger(event, data) {
    if (this.listeners[event]) {
      this.listeners[event].forEach(callback => callback(data))
    }
  }
}

export const wsManager = new WebSocketManager()

