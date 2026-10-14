/** File used to setup axios */
import { API_URLS } from '@/constants/api-urls'
import { redirectToLogin } from '@/router'
import { useAuthStore } from '@/stores/auth'
import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'

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

type RetriableRequestConfig = InternalAxiosRequestConfig & { isRetry?: boolean }
const AUTH_URLS: string[] = [API_URLS.auth.login, API_URLS.auth.refresh, API_URLS.auth.logout]

// On 401 get a new access token with the refresh token and retry the request once,
// log out and go to the login page when the refresh token is no longer valid
api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const authStore = useAuthStore()
    const config = error.config as RetriableRequestConfig | undefined

    if (
      error.response?.status !== 401 ||
      !config ||
      config.isRetry ||
      AUTH_URLS.includes(config.url ?? '')
    ) {
      return Promise.reject(error)
    }

    if (authStore.isAuthenticated && (await authStore.refreshSession())) {
      config.isRetry = true
      return api(config)
    }

    // also reached when another tab logged out, expireSession is then a no
    authStore.expireSession()
    redirectToLogin()
    return Promise.reject(error)
  },
)

export default api
