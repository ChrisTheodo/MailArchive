import { ref } from 'vue'
import { defineStore } from 'pinia'
import { useGetRequest } from '@/composables/useGetRequest'
import { API_URLS } from '@/constants/api-urls'
import { useSnackbarMessagesStore } from './snackbar-messages'
import type { MyMailboxListResponse } from '@/types/me/my-mailbox-list-response'
import type { MyMailboxResponse } from '@/types/me/my-mailbox-response'
import { useDeleteRequest } from '@/composables/useDeleteRequest'
import type { DeleteMailboxRequest } from '@/types/emails/delete-mailbox-request'
import type { MyMailboxCreateRequest } from '@/types/me/my-mailbox-create-request'
import { usePostRequest } from '@/composables/usePostRequest'
import type { MailboxResponse } from '@/types/mailboxes/mailbox-response'

export const useMailboxStore = defineStore('mailbox', () => {
  // STATE
  const mailboxes = ref<MyMailboxResponse[]>([])
  const selectedMailbox = ref<MyMailboxResponse | null>(null)
  const totalMailboxes = ref(0)
  const generatedAtUtc = ref<MyMailboxListResponse['generatedAtUtc'] | null>(null)

  // CONSTANTS
  const snackbarMessagesStore = useSnackbarMessagesStore()

  // REQUESTS
  const listRequest = useGetRequest<MyMailboxListResponse>()
  const deleteRequest = useDeleteRequest()
  const createRequest = usePostRequest<MyMailboxCreateRequest, MailboxResponse>()

  async function fetchMailboxes() {
    const res = await listRequest.execute(API_URLS.me.meMailboxes.mailboxes)
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία ανάκτησης mailbox')
      return false
    }
    mailboxes.value = [...res.mailboxes]
    totalMailboxes.value = res.totalMailboxes
    generatedAtUtc.value = res.generatedAtUtc

    // keep the selected mailbox in sync with fresh stats
    if (selectedMailbox.value) {
      selectedMailbox.value =
        mailboxes.value.find((m) => m.mailboxId === selectedMailbox.value!.mailboxId) ?? null
    }
    return true
  }

  async function fetchMailbox(mailboxId: string) {
    const ok = await fetchMailboxes()
    if (!ok) {
      selectedMailbox.value = null
      return null
    }

    const mailbox = mailboxes.value.find((m) => m.mailboxId === mailboxId) ?? null
    if (!mailbox) {
      snackbarMessagesStore.addErrorMessage('Το mailbox δεν βρέθηκε')
    }
    selectedMailbox.value = mailbox
    return mailbox
  }

  // Keeps the sidebar count right after an email is deleted, without refetching
  function decrementTotalEmails(mailboxId: string) {
    const mailbox = getMailboxById(mailboxId)
    if (mailbox) mailbox.totalEmails = Math.max(0, mailbox.totalEmails - 1)
  }

  async function createMailbox(request: MyMailboxCreateRequest) {
    const res = await createRequest.execute(API_URLS.me.meMailboxes.post, { requestBody: request })
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία δημιουργίας mailbox')
      return null
    }

    await fetchMailboxes()
    snackbarMessagesStore.addSuccessMessage('Το mailbox δημιουργήθηκε επιτυχώς')
    return res
  }

  function getMailboxById(mailboxId: string) {
    return mailboxes.value.find((m) => m.mailboxId === mailboxId) ?? null
  }

  async function deleteMailbox(request: DeleteMailboxRequest) {
    const res = await deleteRequest.execute(API_URLS.mailboxes.delete(request))
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία διαγραφής mailbox')
      return false
    }

    mailboxes.value = mailboxes.value.filter((m) => m.mailboxId !== request.id)
    totalMailboxes.value = mailboxes.value.length

    if (selectedMailbox.value?.mailboxId === request.id) {
      selectedMailbox.value = null
    }

    snackbarMessagesStore.addSuccessMessage('Το mailbox διαγράφηκε επιτυχώς')
    return true
  }

  function $reset() {
    mailboxes.value = []
    selectedMailbox.value = null
    totalMailboxes.value = 0
    generatedAtUtc.value = null
  }

  return {
    mailboxes,
    selectedMailbox,
    totalMailboxes,
    generatedAtUtc,
    isLoadingMailboxes: listRequest.isLoading,
    mailboxesError: listRequest.error,
    isDeletingMailbox: deleteRequest.isLoading,
    deleteMailboxError: deleteRequest.error,
    isCreatingMailbox: createRequest.isLoading,
    createMailboxError: createRequest.error,
    fetchMailboxes,
    fetchMailbox,
    createMailbox,
    getMailboxById,
    deleteMailbox,
    $reset,
    decrementTotalEmails,
  }
})
