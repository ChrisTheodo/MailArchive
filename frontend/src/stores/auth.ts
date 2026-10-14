import { defineStore } from 'pinia'
import { ref, readonly, computed } from 'vue'
import { usePostRequest } from '@/composables/usePostRequest'
import { API_URLS } from '@/constants/api-urls'
import type { LoginResponse } from '@/types/auth/login-response'
import type { LoginRequest } from '@/types/auth/login-request'
import type { LogoutRequest } from '@/types/auth/logout-request'
import { STORAGE_KEYS } from '@/constants/storage-keys'
import { useEmailStore } from './email'
import { useMailboxStore } from './mailbox'
import { useImportStore } from './import'
import { until, useLocalStorage } from '@vueuse/core'
import { useUserStore } from './user'
import { useSnackbarMessagesStore } from './snackbar-messages'
import type { RefreshTokenRequest } from '@/types/auth/refresh-token-request'

export const useAuthStore = defineStore('auth', () => {
  // STATE
  const accessToken = useLocalStorage<string | null>(STORAGE_KEYS.accessToken, null)
  const tokenType = useLocalStorage<string | null>(STORAGE_KEYS.tokenType, null)
  const refreshToken = useLocalStorage<string | null>(STORAGE_KEYS.refreshToken, null)
  const expiresAt = useLocalStorage<string | null>(STORAGE_KEYS.accessTokenExpiresAt, null)
  const refreshExpiresAt = useLocalStorage<string | null>(STORAGE_KEYS.refreshTokenExpiresAt, null)
  const isAuthenticated = computed(() => accessToken.value !== null && tokenType.value !== null)

  // CONSTANTS
  const WAIT_FOR_TOKENS_FROM_OTHER_TABS = 1000

  const { execute: loginExecute, isLoading: isLoginLoading } = usePostRequest<
    LoginRequest,
    LoginResponse
  >()

  const { execute: logoutExecute, isLoading: isLogoutLoading } = usePostRequest<
    LogoutRequest,
    void
  >()

  const { execute: refreshExecute } = usePostRequest<RefreshTokenRequest, LoginResponse>()

  let refreshPromise: Promise<boolean> | null = null

  function setSession(res: LoginResponse) {
    accessToken.value = res.accessToken
    tokenType.value = res.tokenType
    refreshToken.value = res.refreshToken
    expiresAt.value = res.expiresAtUtc
    refreshExpiresAt.value = res.refreshTokenExpiresAtUtc
  }

  function clearSession() {
    accessToken.value = null
    tokenType.value = null
    refreshToken.value = null
    expiresAt.value = null
    refreshExpiresAt.value = null
  }

  // necessary since this is an SPA tabs hold store values
  function resetUserStores() {
    useEmailStore().$reset()
    useMailboxStore().$reset()
    useImportStore().$reset()
    useUserStore().$reset()
  }

  async function login(email: string, password: string) {
    const res = await loginExecute(API_URLS.auth.login, { requestBody: { email, password } })

    if (!res) {
      clearSession()
      return false
    }

    setSession(res)
    return true
  }

  async function logout() {
    try {
      await logoutExecute(API_URLS.auth.logout, {
        requestBody: { refreshToken: refreshToken.value },
      })
    } finally {
      clearSession()
      resetUserStores()
    }
  }

  function isTokenExpired() {
    if (!accessToken.value || !expiresAt.value) return false

    return Date.parse(expiresAt.value) <= Date.now()
  }

  function isRefreshTokenExpired() {
    if (!refreshToken.value || !refreshExpiresAt.value) return false

    return Date.parse(refreshExpiresAt.value) <= Date.now()
  }

  function expireSession() {
    if (!isAuthenticated.value) return

    clearSession()
    resetUserStores()
    useSnackbarMessagesStore().addErrorMessage('Η συνεδρία έληξε, συνδεθείτε ξανά')
  }

  // refreshes an expired access token, or logs out locally when that isn't possible
  async function ensureValidSession() {
    if (!isTokenExpired()) return true
    if (await refreshSession()) return true

    expireSession()
    return false
  }

  // gets a new access token with the refresh token, false when the refresh token is no longer valid
  function refreshSession() {
    refreshPromise ??= (async () => {
      const sentRefreshToken = refreshToken.value
      if (!sentRefreshToken || isRefreshTokenExpired()) return false

      const res = await refreshExecute(API_URLS.auth.refresh, {
        requestBody: { refreshToken: sentRefreshToken },
      })
      if (res) {
        setSession(res)
        return true
      }

      // another tab may have refreshed at the same moment, which revokes this refresh token.
      // its new tokens reach this tab through the storage event, so wait briefly for them
      await until(refreshToken).not.toBe(sentRefreshToken, {
        timeout: WAIT_FOR_TOKENS_FROM_OTHER_TABS,
      })
      return refreshToken.value !== null && refreshToken.value !== sentRefreshToken
    })().finally(() => {
      refreshPromise = null
    })

    return refreshPromise
  }

  return {
    accessToken: readonly(accessToken),
    tokenType: readonly(tokenType),
    isLoginLoading,
    isLogoutLoading,
    login,
    logout,
    isAuthenticated,
    ensureValidSession,
    expireSession,
    refreshSession,
  }
})
