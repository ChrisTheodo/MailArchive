<script setup lang="ts">
import { useSnackbarMessagesStore } from '@/stores/snackbar-messages'
import { useAuthStore } from './stores/auth'
import { useEventListener } from '@vueuse/core'
import { redirectToLogin } from '@/router'

const snackbarMessagesStore = useSnackbarMessagesStore()
const authStore = useAuthStore()

// a tab left open for a long time so check the session when the user comes back to it
// this also covers a logout in another tab, the tokens are synced but the page isn't
useEventListener(document, 'visibilitychange', async () => {
  if (document.visibilityState != 'visible') return

  if (!authStore.isAuthenticated || !(await authStore.ensureValidSession())) redirectToLogin()
})
</script>

<template>
  <v-app>
    <router-view />
    <v-snackbar-queue
      v-model="snackbarMessagesStore.queue"
      timeout="3000"
      total-messages="5"
      location="bottom end"
      timer="bottom"
      closable
      data-testid="snackbar-queue"
    />
  </v-app>
</template>
