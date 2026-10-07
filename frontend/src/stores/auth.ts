import { defineStore } from 'pinia'
import { ref, readonly, computed } from 'vue'
import { usePostRequest } from '@/composables/usePostRequest'
import { API_URLS } from '@/constants/api-urls'
import { type RawAxiosRequestHeaders } from 'axios'
import type { LoginResponse } from '@/types/auth/login-response'
import type { LoginRequest } from '@/types/auth/login-request'
import type { LogoutRequest } from '@/types/auth/logout-request'

export const useAuthStore = defineStore('auth', () => {
  // STATE
  const accessToken = ref<string | null>(null)
  const tokenType = ref<string | null>(null)
  const refreshToken = ref<string | null>(null)
  const isAuthenticated = computed(() => accessToken.value !== null && tokenType.value !== null)

  const {
    execute: loginExecute,
    error,
    isLoading: loginLoading,
  } = usePostRequest<LoginRequest, LoginResponse>(API_URLS.auth.login)
  const { execute: logoutExecute, isLoading: logoutLoading } = usePostRequest<LogoutRequest, void>(
    API_URLS.auth.logout,
  )

  function clearSession() {
    accessToken.value = null
    tokenType.value = null
    refreshToken.value = null
  }

  async function login() {
    const res = await loginExecute({ email: 'admin@example.com', password: 'Admin123!' })
    if (!res) {
      clearSession()
      return false
    }

    accessToken.value = res.accessToken
    tokenType.value = res.tokenType
    refreshToken.value = res.refreshToken
    return true
  }

  async function logout() {
    await logoutExecute({ refreshToken: refreshToken.value })
    clearSession()
  }

  return {
    accessToken: readonly(accessToken),
    tokenType: readonly(tokenType),
    isLoginLoading: loginLoading,
    isLogoutLoading: logoutLoading,
    login,
    logout,
    loginError: error,
    isAuthenticated,
  }
})
