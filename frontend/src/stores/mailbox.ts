import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { Mailbox } from '@/types/entities/mailbox'
import { useGetRequest } from '@/composables/useGetRequest'
import type { MyMailboxListResponse } from '@/types/me/my-mailbox-list-response'
import { API_URLS } from '@/constants/api-urls'
import { useSnackbarMessagesStore } from './snackbar-messages'
import type { MyMailboxResponse } from '@/types/me/my-mailbox-response'

export const useMailboxStore = defineStore('mailbox', () => {
  // STATE
  const mailboxes = ref<MyMailboxResponse[]>([])
  const snackbarMessagesStore = useSnackbarMessagesStore()

  // REQUESTS
  const listRequest = useGetRequest<MyMailboxListResponse>()

  async function fetchMailboxes() {
    const res = await listRequest.execute(API_URLS.me.meMailboxes.mailboxes)
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία ανάκτησης mailbox')
      return false
    }
    mailboxes.value = [...res.mailboxes]
    return true
  }

  async function fetchMailbox(mailboxId: string) {}

  async function deleteMailbox(mailboxId: string) {}

  function $reset() {
    mailboxes.value = []
  }

  return {
    mailboxes,
    isLoadingMailboxes: listRequest.isLoading,
    mailboxesError: listRequest.error,
    fetchMailboxes,
    fetchMailbox,
    deleteMailbox,
    $reset,
  }
})
