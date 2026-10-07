import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { vuetify } from '../src/plugins/vuetify'

import App from './App.vue'
import router from './router'
import 'unfonts.css'
import 'virtual:uno.css'
import './styles/scrollbar.css'

const app = createApp(App)

app.use(vuetify)
app.use(createPinia())
app.use(router)

app.mount('#app')
