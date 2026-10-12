import { defineStore } from 'pinia'
import { ref, readonly, computed } from 'vue'
import { usePostRequest } from '@/composables/usePostRequest'
import { API_URLS } from '@/constants/api-urls'
import type { LoginResponse } from '@/types/auth/login-response'
import type { LoginRequest } from '@/types/auth/login-request'
import type { LogoutRequest } from '@/types/auth/logout-request'
import { STORAGE_KEYS } from '@/constants/storage-keys'
import { useEmailStore } from './email'
import { useEmailSettingsStore } from './email-settings'

export const useAuthStore = defineStore('auth', () => {
  // STATE
  const accessToken = ref<string | null>(localStorage.getItem(STORAGE_KEYS.accessToken))
  const tokenType = ref<string | null>(localStorage.getItem(STORAGE_KEYS.tokenType))
  const refreshToken = ref<string | null>(localStorage.getItem(STORAGE_KEYS.refreshToken))
  const isAuthenticated = computed(() => accessToken.value !== null && tokenType.value !== null)

  // CONSTANTS
  const { execute: loginExecute, isLoading: isLoginLoading } = usePostRequest<
    LoginRequest,
    LoginResponse
  >(API_URLS.auth.login)

  const { execute: logoutExecute, isLoading: isLogoutLoading } = usePostRequest<
    LogoutRequest,
    void
  >(API_URLS.auth.logout)

  function clearSession() {
    accessToken.value = null
    tokenType.value = null
    refreshToken.value = null
    localStorage.removeItem(STORAGE_KEYS.accessToken)
    localStorage.removeItem(STORAGE_KEYS.tokenType)
    localStorage.removeItem(STORAGE_KEYS.refreshToken)
  }

  async function login(email: string, password: string) {
    const res = await loginExecute({ email, password })

    if (!res) {
      clearSession()
      return false
    }

    accessToken.value = res.accessToken
    tokenType.value = res.tokenType
    refreshToken.value = res.refreshToken
    localStorage.setItem(STORAGE_KEYS.accessToken, accessToken.value)
    localStorage.setItem(STORAGE_KEYS.tokenType, tokenType.value)
    localStorage.setItem(STORAGE_KEYS.refreshToken, refreshToken.value)
    return true
  }

  async function logout() {
    try {
      await logoutExecute({ refreshToken: refreshToken.value })
    } finally {
      clearSession()
      useEmailStore().$reset()
      useEmailSettingsStore().$reset()
    }
  }

  return {
    accessToken: readonly(accessToken),
    tokenType: readonly(tokenType),
    isLoginLoading,
    isLogoutLoading,
    login,
    logout,
    isAuthenticated,
  }
})
