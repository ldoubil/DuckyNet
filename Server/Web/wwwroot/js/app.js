const { createApp } = Vue;

createApp({
    data() {
        return {
            currentTab: 'overview',
            tabs: [
                { id: 'overview', name: '总览' },
                { id: 'rooms', name: '房间' },
                { id: 'players', name: '玩家' },
                { id: 'scenes', name: '场景' }
            ],
            loading: false,
            serverTime: '',
            overview: {
                onlinePlayers: 0,
                totalRooms: 0,
                totalNpcs: 0,
                uptime: '运行中'
            },
            rooms: [],
            players: [],
            scenes: [],
            selectedRoom: null,
            roomPlayers: [],
            selectedScene: null,
            sceneDetail: null,
            refreshInterval: null
        };
    },
    mounted() {
        this.updateServerTime();
        this.loadData();
        
        // 每5秒刷新数据
        this.refreshInterval = setInterval(() => {
            this.loadData();
        }, 5000);
        
        // 每秒更新时间
        setInterval(() => {
            this.updateServerTime();
        }, 1000);
    },
    unmounted() {
        if (this.refreshInterval) {
            clearInterval(this.refreshInterval);
        }
    },
    methods: {
        updateServerTime() {
            const now = new Date();
            this.serverTime = now.toLocaleString('zh-CN');
        },
        
        async loadData() {
            if (this.currentTab === 'overview') {
                await this.loadOverview();
            } else if (this.currentTab === 'rooms') {
                await this.loadRooms();
            } else if (this.currentTab === 'players') {
                await this.loadPlayers();
            } else if (this.currentTab === 'scenes') {
                await this.loadScenes();
            }
        },
        
        async loadOverview() {
            try {
                const response = await axios.get('/api/dashboard/overview');
                this.overview = response.data;
            } catch (error) {
                console.error('加载概览数据失败:', error);
            }
        },
        
        async loadRooms() {
            try {
                const response = await axios.get('/api/rooms');
                this.rooms = response.data;
            } catch (error) {
                console.error('加载房间列表失败:', error);
            }
        },
        
        async loadPlayers() {
            try {
                const response = await axios.get('/api/players');
                this.players = response.data;
            } catch (error) {
                console.error('加载玩家列表失败:', error);
            }
        },
        
        async loadScenes() {
            try {
                const response = await axios.get('/api/scenes');
                this.scenes = response.data;
            } catch (error) {
                console.error('加载场景列表失败:', error);
            }
        },
        
        async selectRoom(room) {
            this.selectedRoom = room;
            try {
                const response = await axios.get(`/api/rooms/${room.roomId}`);
                this.roomPlayers = response.data.players;
            } catch (error) {
                console.error('加载房间详情失败:', error);
            }
        },
        
        async selectScene(scene) {
            this.selectedScene = scene;
            try {
                const response = await axios.get(`/api/scenes/${scene.sceneName}/${scene.subSceneName}`);
                this.sceneDetail = response.data;
            } catch (error) {
                console.error('加载场景详情失败:', error);
            }
        },
        
        formatTime(timeStr) {
            const date = new Date(timeStr);
            return date.toLocaleString('zh-CN');
        }
    },
    watch: {
        currentTab(newTab) {
            this.selectedRoom = null;
            this.selectedScene = null;
            this.sceneDetail = null;
            this.loadData();
        }
    }
}).mount('#app');

