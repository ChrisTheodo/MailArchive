import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type { CurrentUserResponse } from '@/types/auth/current-user-response'
import { useGetRequest } from '@/composables/useGetRequest'
import { API_URLS } from '@/constants/api-urls'
import { useSnackbarMessagesStore } from './snackbar-messages'
import { UserRole } from '@/types/users/user-role'

export const useUserStore = defineStore('user', () => {
  // STATE
  const currentUser = ref<CurrentUserResponse | null>(null)
  const isAdmin = computed(() => currentUser.value?.role === UserRole.Admin)

  // CONSTANTS
  const meRequest = useGetRequest<CurrentUserResponse>()

  // METHODS
  async function fetchMe() {
    const res = await meRequest.execute(API_URLS.me.me)
    if (!res) {
      useSnackbarMessagesStore().addErrorMessage('Αποτυχία λήψης δεδομένων χρήστη')
      return
    }

    currentUser.value = res
  }

  function $reset() {
    currentUser.value = null
  }

  return {
    fetchMe,
    isMeLoading: meRequest.isLoading,
    isMeError: meRequest.error,
    currentUser,
    isAdmin,
    $reset,
  }
})
