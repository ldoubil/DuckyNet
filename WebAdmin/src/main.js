import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'

// Vuetify
import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import '@mdi/font/css/materialdesignicons.css'

const vuetify = createVuetify({
  components,
  directives,
  theme: {
    defaultTheme: 'dark',
    themes: {
      dark: {
        colors: {
          primary: '#1E88E5',
          secondary: '#26C6DA',
          accent: '#9C27B0',
          error: '#F44336',
          warning: '#FF9800',
          info: '#2196F3',
          success: '#4CAF50',
          background: '#121212',
          surface: '#1E1E1E'
        }
      }
    }
  }
})

const pinia = createPinia()
const app = createApp(App)

app.use(vuetify)
app.use(pinia)
app.mount('#app')

