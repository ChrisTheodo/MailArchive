<script setup lang="ts">
import { useSnackbarMessagesStore } from '@/stores/snackbar-messages'

const snackbar = useSnackbarMessagesStore()
</script>

<template>
  <v-app>
    <router-view />
    <v-snackbar-queue
      v-model="snackbar.queue"
      timeout="3000"
      total-messages="5"
      location="bottom end"
    />
  </v-app>
</template>
