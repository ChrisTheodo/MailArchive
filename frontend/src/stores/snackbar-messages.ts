import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { SnackbarQueueMessage } from 'vuetify'

export const useSnackbarMessagesStore = defineStore('snackbar-message', () => {
  const queue = ref<SnackbarQueueMessage[]>([])

  function add(message: SnackbarQueueMessage) {
    queue.value.push(message)
  }

  function addErrorMessage(text: string) {
    add({ text, color: 'error', prependIcon: 'i-mdi-alert' })
  }

  function addSuccessMessage(text: string) {
    add({ text, color: 'success', prependIcon: 'i-mdi-check-circle' })
  }

  function addLoaderMessage(text: string) {
    add({ text, color: 'info', loading: true })
  }

  function $reset() {
    queue.value = []
  }

  return { queue, add, addErrorMessage, addSuccessMessage, addLoaderMessage, $reset }
})
