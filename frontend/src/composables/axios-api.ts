/** File used to setup axios */
import { useAuthStore } from '@/stores/auth'
import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VUE_APP_BASEURL,
  withCredentials: true,
})

api.interceptors.request.use((config) => {
  const authStore = useAuthStore()
  if (authStore.accessToken && authStore.tokenType) {
    config.headers.Authorization = `${authStore.tokenType} ${authStore.accessToken}`
  }

  // FormData needs the browser to set multipart/form-data with its boundary
  if (!(config.data instanceof FormData)) {
    config.headers['Content-Type'] = 'application/json'
  }

  return config
})

export default api
