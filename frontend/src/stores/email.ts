import { ref } from 'vue'
import { defineStore } from 'pinia'
import type { EmailListResponse } from '@/types/emails/email-list-response'
import type { PagedResult } from '@/types/common/paged-result'
import type { EmailQueryParameters } from '@/types/emails/email-query-parameters'
import { useGetRequest } from '@/composables/useGetRequest'
import { API_URLS } from '@/constants/api-urls'
import type { EmailDetailsResponse } from '@/types/emails/email-details-response'
import type { EmailAttachmentResponse } from '@/types/emails/email-attachment-response'
import { useDeleteRequest } from '@/composables/useDeleteRequest'
import type { DeleteEmailRequest } from '@/types/emails/delete-email-request'
import { useSnackbarMessagesStore } from './snackbar-messages'
import { useMailboxStore } from './mailbox'

type EmailQuery = Partial<EmailQueryParameters>

export const useEmailStore = defineStore('email', () => {
  // STATE
  const emails = ref<PagedResult<EmailListResponse> | null>(null)
  const emailsFromSearch = ref<PagedResult<EmailListResponse> | null>(null)
  const search = ref('')

  // CONSTANTS
  const snackbarMessagesStore = useSnackbarMessagesStore()
  const mailboxStore = useMailboxStore()
  const listRequest = useGetRequest<PagedResult<EmailListResponse>>()
  const searchRequest = useGetRequest<PagedResult<EmailListResponse>>()
  const detailsRequest = useGetRequest<EmailDetailsResponse>()
  const attachmentsRequest = useGetRequest<EmailAttachmentResponse[]>()
  const deleteRequest = useDeleteRequest()

  // Only the most recent request may write its result
  let latestListRequest = 0
  let latestSearchRequest = 0

  // METHODS
  async function fetchEmails(mailboxId: string, query: EmailQuery = {}) {
    const requestId = ++latestListRequest
    const res = await listRequest.execute(API_URLS.emails.list, {
      params: { ...query, mailboxId },
    })
    if (requestId !== latestListRequest) return null
    emails.value = res
    return res
  }

  async function fetchAllEmails(query: EmailQuery = {}) {
    const requestId = ++latestListRequest
    const res = await listRequest.execute(API_URLS.emails.list, { params: query })
    if (requestId !== latestListRequest) return null
    emails.value = res
    return res
  }

  async function searchEmails(searchString: string, query: EmailQuery = {}) {
    const requestId = ++latestSearchRequest
    const res = await searchRequest.execute(API_URLS.emails.list, {
      params: { ...query, search: searchString },
    })
    if (requestId !== latestSearchRequest) return null
    emailsFromSearch.value = res
    return res
  }

  async function fetchEmail(emailId: string) {
    return detailsRequest.execute(API_URLS.emails.getById(emailId))
  }

  async function fetchAttachments(emailId: string) {
    return attachmentsRequest.execute(API_URLS.emails.attachments(emailId))
  }

  async function deleteEmail(request: DeleteEmailRequest) {
    const ok = await deleteRequest.execute(API_URLS.emails.delete(request))
    if (!ok) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία διαγραφής email')
      return false
    }

    const mailboxId = findMailboxId(request.id)
    if (mailboxId) mailboxStore.decrementTotalEmails(mailboxId)
    emails.value = withoutEmail(emails.value, request.id)
    emailsFromSearch.value = withoutEmail(emailsFromSearch.value, request.id)
    snackbarMessagesStore.addSuccessMessage('Το email διαγράφηκε')
    return true
  }

  function findMailboxId(emailId: string) {
    const all = [...(emails.value?.items ?? []), ...(emailsFromSearch.value?.items ?? [])]
    return all.find((e) => e.id === emailId)?.mailboxId ?? null
  }

  function withoutEmail(page: PagedResult<EmailListResponse> | null, emailId: string) {
    if (!page || !page.items.some((e) => e.id === emailId)) return page
    return {
      ...page,
      items: page.items.filter((e) => e.id !== emailId),
      totalCount: page.totalCount - 1,
    }
  }

  function clearSearch() {
    search.value = ''
    emailsFromSearch.value = null
  }

  function $reset() {
    emails.value = null
    emailsFromSearch.value = null
    search.value = ''
  }

  return {
    emails,
    emailsFromSearch,
    search,
    isLoadingEmails: listRequest.isLoading,
    emailsError: listRequest.error,
    isSearching: searchRequest.isLoading,
    searchError: searchRequest.error,
    isLoadingEmail: detailsRequest.isLoading,
    emailError: detailsRequest.error,
    isLoadingAttachments: attachmentsRequest.isLoading,
    attachmentsError: attachmentsRequest.error,
    isDeletingEmail: deleteRequest.isLoading,
    deleteError: deleteRequest.error,
    fetchEmails,
    fetchAllEmails,
    searchEmails,
    fetchEmail,
    fetchAttachments,
    deleteEmail,
    clearSearch,
    $reset,
  }
})
