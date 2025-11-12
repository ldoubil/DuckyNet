<template>
  <div>
    <v-card>
      <v-card-title class="d-flex justify-space-between">
        <span>场景地图 (2D俯视图)</span>
        <v-btn-group density="compact">
          <v-btn @click="zoomIn" icon="mdi-plus"></v-btn>
          <v-btn @click="zoomOut" icon="mdi-minus"></v-btn>
          <v-btn @click="resetView" icon="mdi-refresh"></v-btn>
        </v-btn-group>
      </v-card-title>
      <v-card-text>
        <canvas 
          ref="canvas" 
          :width="canvasWidth" 
          :height="canvasHeight"
          @mousedown="onMouseDown"
          @mousemove="onMouseMove"
          @mouseup="onMouseUp"
          @wheel="onWheel"
          style="border: 1px solid #666; cursor: grab;">
        </canvas>
        
        <v-row class="mt-2">
          <v-col cols="12" md="6">
            <div class="d-flex align-center">
              <v-icon color="primary" class="mr-2">mdi-account</v-icon>
              <span>玩家 ({{ players.length }})</span>
            </div>
          </v-col>
          <v-col cols="12" md="6">
            <div class="d-flex align-center">
              <v-icon color="warning" class="mr-2">mdi-robot</v-icon>
              <span>NPC ({{ npcs.length }})</span>
            </div>
          </v-col>
        </v-row>
        
        <!-- 实体列表 -->
        <v-expansion-panels class="mt-3">
          <v-expansion-panel>
            <v-expansion-panel-title>
              <v-icon left>mdi-format-list-bulleted</v-icon>
              实体详情列表
            </v-expansion-panel-title>
            <v-expansion-panel-text>
              <v-tabs v-model="tab">
                <v-tab value="players">玩家</v-tab>
                <v-tab value="npcs">NPC</v-tab>
              </v-tabs>
              
              <v-window v-model="tab">
                <v-window-item value="players">
                  <v-list>
                    <v-list-item 
                      v-for="player in players" 
                      :key="player.steamId"
                      @click="focusEntity(player, 'player')">
                      <template v-slot:prepend>
                        <v-icon color="primary">mdi-account</v-icon>
                      </template>
                      <v-list-item-title>{{ player.steamName }}</v-list-item-title>
                      <v-list-item-subtitle v-if="player.position">
                        X: {{ player.position.x.toFixed(1) }}, 
                        Y: {{ player.position.y.toFixed(1) }}, 
                        Z: {{ player.position.z.toFixed(1) }}
                      </v-list-item-subtitle>
                    </v-list-item>
                  </v-list>
                </v-window-item>
                
                <v-window-item value="npcs">
                  <v-list>
                    <v-list-item 
                      v-for="npc in npcs" 
                      :key="npc.npcId"
                      @click="focusEntity(npc, 'npc')">
                      <template v-slot:prepend>
                        <v-icon color="warning">mdi-robot</v-icon>
                      </template>
                      <v-list-item-title>{{ npc.npcType }}</v-list-item-title>
                      <v-list-item-subtitle>
                        血量: {{ npc.maxHealth }} | 
                        位置: ({{ npc.position.x.toFixed(1) }}, {{ npc.position.y.toFixed(1) }}, {{ npc.position.z.toFixed(1) }})
                      </v-list-item-subtitle>
                    </v-list-item>
                  </v-list>
                </v-window-item>
              </v-window>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </v-card-text>
    </v-card>
  </div>
</template>

<script>
import { ref, onMounted, onUnmounted, watch, nextTick } from 'vue'

export default {
  name: 'SceneMap',
  props: {
    players: {
      type: Array,
      default: () => []
    },
    npcs: {
      type: Array,
      default: () => []
    }
  },
  setup(props) {
    const canvas = ref(null)
    const canvasWidth = 800
    const canvasHeight = 600
    const tab = ref('players')
    let autoRefreshTimer = null
    
    // 视图控制
    const offsetX = ref(0)
    const offsetY = ref(0)
    const scale = ref(1)
    const isDragging = ref(false)
    const lastMouseX = ref(0)
    const lastMouseY = ref(0)
    
    // 地图范围自动计算
    const bounds = ref({
      minX: -100,
      maxX: 100,
      minZ: -100,
      maxZ: 100
    })
    
    const calculateBounds = () => {
      const allEntities = [
        ...props.players.filter(p => p.position).map(p => p.position),
        ...props.npcs.map(n => n.position)
      ]
      
      if (allEntities.length === 0) return
      
      const xs = allEntities.map(e => e.x)
      const zs = allEntities.map(e => e.z)
      
      bounds.value = {
        minX: Math.min(...xs, -100),
        maxX: Math.max(...xs, 100),
        minZ: Math.min(...zs, -100),
        maxZ: Math.max(...zs, 100)
      }
      
      // 添加边距
      const marginX = (bounds.value.maxX - bounds.value.minX) * 0.1
      const marginZ = (bounds.value.maxZ - bounds.value.minZ) * 0.1
      bounds.value.minX -= marginX
      bounds.value.maxX += marginX
      bounds.value.minZ -= marginZ
      bounds.value.maxZ += marginZ
    }
    
    const worldToScreen = (x, z) => {
      const rangeX = bounds.value.maxX - bounds.value.minX
      const rangeZ = bounds.value.maxZ - bounds.value.minZ
      
      const screenX = ((x - bounds.value.minX) / rangeX) * canvasWidth * scale.value + offsetX.value
      const screenY = ((z - bounds.value.minZ) / rangeZ) * canvasHeight * scale.value + offsetY.value
      
      return { x: screenX, y: screenY }
    }
    
    const drawMap = () => {
      if (!canvas.value) return
      
      const ctx = canvas.value.getContext('2d')
      if (!ctx) return
      
      // 清空画布
      ctx.clearRect(0, 0, canvasWidth, canvasHeight)
      
      // 背景
      ctx.fillStyle = '#1E1E1E'
      ctx.fillRect(0, 0, canvasWidth, canvasHeight)
      
      // 网格
      ctx.strokeStyle = '#333'
      ctx.lineWidth = 1
      const gridSize = 50
      for (let i = bounds.value.minX; i <= bounds.value.maxX; i += gridSize) {
        const pos = worldToScreen(i, bounds.value.minZ)
        const pos2 = worldToScreen(i, bounds.value.maxZ)
        ctx.beginPath()
        ctx.moveTo(pos.x, pos.y)
        ctx.lineTo(pos2.x, pos2.y)
        ctx.stroke()
      }
      for (let i = bounds.value.minZ; i <= bounds.value.maxZ; i += gridSize) {
        const pos = worldToScreen(bounds.value.minX, i)
        const pos2 = worldToScreen(bounds.value.maxX, i)
        ctx.beginPath()
        ctx.moveTo(pos.x, pos.y)
        ctx.lineTo(pos2.x, pos2.y)
        ctx.stroke()
      }
      
      // 原点标记
      const origin = worldToScreen(0, 0)
      ctx.strokeStyle = '#666'
      ctx.lineWidth = 2
      ctx.beginPath()
      ctx.moveTo(origin.x - 10, origin.y)
      ctx.lineTo(origin.x + 10, origin.y)
      ctx.moveTo(origin.x, origin.y - 10)
      ctx.lineTo(origin.x, origin.y + 10)
      ctx.stroke()
      
      // 绘制NPC（固定图标大小）
      props.npcs.forEach(npc => {
        const pos = worldToScreen(npc.position.x, npc.position.z)
        
        // NPC圆形（固定大小8px）
        ctx.fillStyle = '#FF9800'
        ctx.beginPath()
        ctx.arc(pos.x, pos.y, 8, 0, Math.PI * 2)
        ctx.fill()
        
        ctx.strokeStyle = '#FFC107'
        ctx.lineWidth = 2
        ctx.stroke()
        
        // NPC名称（固定大小12px）
        ctx.fillStyle = '#FFF'
        ctx.font = '12px Arial'
        ctx.fillText(npc.npcType, pos.x + 12, pos.y + 4)
        
        // 血量条（固定大小）
        const barWidth = 40
        const barHeight = 4
        ctx.fillStyle = '#333'
        ctx.fillRect(pos.x - barWidth/2, pos.y - 15, barWidth, barHeight)
        ctx.fillStyle = '#4CAF50'
        ctx.fillRect(pos.x - barWidth/2, pos.y - 15, barWidth, barHeight)
      })
      
      // 绘制玩家（固定图标大小）
      props.players.forEach(player => {
        if (!player.position) return
        
        const pos = worldToScreen(player.position.x, player.position.z)
        
        // 玩家三角形（固定大小）
        ctx.fillStyle = '#2196F3'
        ctx.beginPath()
        ctx.moveTo(pos.x, pos.y - 10)
        ctx.lineTo(pos.x - 8, pos.y + 6)
        ctx.lineTo(pos.x + 8, pos.y + 6)
        ctx.closePath()
        ctx.fill()
        
        ctx.strokeStyle = '#64B5F6'
        ctx.lineWidth = 2
        ctx.stroke()
        
        // 玩家名称（固定大小12px）
        ctx.fillStyle = '#FFF'
        ctx.font = '12px Arial'
        ctx.fillText(player.steamName, pos.x + 12, pos.y + 4)
      })
    }
    
    const zoomIn = () => {
      scale.value = Math.min(scale.value * 1.2, 5)
      drawMap()
    }
    
    const zoomOut = () => {
      scale.value = Math.max(scale.value / 1.2, 0.1)
      drawMap()
    }
    
    const resetView = () => {
      offsetX.value = 0
      offsetY.value = 0
      scale.value = 1
      calculateBounds()
      drawMap()
    }
    
    const focusEntity = (entity, type) => {
      const pos = type === 'player' && entity.position 
        ? entity.position 
        : type === 'npc' 
        ? entity.position 
        : null
      
      if (!pos) return
      
      // 将实体移到画布中心
      const screenPos = worldToScreen(pos.x, pos.z)
      offsetX.value += canvasWidth / 2 - screenPos.x
      offsetY.value += canvasHeight / 2 - screenPos.y
      
      drawMap()
    }
    
    const onMouseDown = (e) => {
      isDragging.value = true
      lastMouseX.value = e.clientX
      lastMouseY.value = e.clientY
      canvas.value.style.cursor = 'grabbing'
    }
    
    const onMouseMove = (e) => {
      if (!isDragging.value) return
      
      const dx = e.clientX - lastMouseX.value
      const dy = e.clientY - lastMouseY.value
      
      offsetX.value += dx
      offsetY.value += dy
      
      lastMouseX.value = e.clientX
      lastMouseY.value = e.clientY
      
      drawMap()
    }
    
    const onMouseUp = () => {
      isDragging.value = false
      canvas.value.style.cursor = 'grab'
    }
    
    const onWheel = (e) => {
      e.preventDefault()
      
      const delta = e.deltaY > 0 ? 0.9 : 1.1
      const newScale = scale.value * delta
      
      if (newScale < 0.1 || newScale > 5) return
      
      // 以鼠标位置为中心缩放
      const rect = canvas.value.getBoundingClientRect()
      const mouseX = e.clientX - rect.left
      const mouseY = e.clientY - rect.top
      
      offsetX.value = mouseX - (mouseX - offsetX.value) * delta
      offsetY.value = mouseY - (mouseY - offsetY.value) * delta
      scale.value = newScale
      
      drawMap()
    }
    
    onMounted(() => {
      calculateBounds()
      nextTick(() => {
        drawMap()
      })
      
      // 自动刷新地图（每0.1秒）
      autoRefreshTimer = setInterval(() => {
        drawMap()
      }, 100)
    })
    
    onUnmounted(() => {
      if (autoRefreshTimer) {
        clearInterval(autoRefreshTimer)
      }
    })
    
    watch(() => [props.players, props.npcs], () => {
      calculateBounds()
      drawMap()
    }, { deep: true, immediate: false })
    
    return {
      canvas,
      canvasWidth,
      canvasHeight,
      tab,
      zoomIn,
      zoomOut,
      resetView,
      focusEntity,
      onMouseDown,
      onMouseMove,
      onMouseUp,
      onWheel
    }
  }
}
</script>

<style scoped>
canvas {
  display: block;
  max-width: 100%;
}
</style>

