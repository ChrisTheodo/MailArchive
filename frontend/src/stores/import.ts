import { ref } from 'vue'
import { defineStore } from 'pinia'
import { useGetRequest } from '@/composables/useGetRequest'
import { usePostRequest } from '@/composables/usePostRequest'
import { API_URLS } from '@/constants/api-urls'
import { useSnackbarMessagesStore } from './snackbar-messages'
import type { MyImportResponse } from '@/types/me/my-import-response'
import type { MyImportListResponse } from '@/types/me/my-import-list-response'
import type { MyImportProgressResponse } from '@/types/me/my-import-progress-response'
import type { MeImportsParams } from '@/types/me/me-imports-params'
import type { MeImportActionsCancelRequest } from '@/types/me/me-import-actions-cancel-request'
import type { MeImportUploadRequest } from '@/types/me/me-import-upload-request'
import type { ImportGetByIdRequest } from '@/types/imports/import-get-by-id-request'
import type { ImportBatchResponse } from '@/types/imports/import-batch-response'
import type { ImportBatchStatus } from '@/types/imports/import-batch-status'
import type { ImportError } from '@/types/entities/import-error'

// POST api/me/imports/{id}/process/background returns an anonymous object
interface ProcessImportBackgroundResponse {
  importBatchId: string
  status: ImportBatchStatus
}

// GET api/me/imports/{id}/errors does not include the navigation property
type MyImportErrorResponse = Omit<ImportError, 'importBatch'>

export const useImportStore = defineStore('import', () => {
  // STATE
  const imports = ref<MyImportResponse[]>([])
  const selectedImport = ref<ImportBatchResponse | null>(null)
  const totalImports = ref(0)
  const page = ref(1)
  const pageSize = ref(0)
  const totalPages = ref(0)
  const generatedAtUtc = ref<MyImportListResponse['generatedAtUtc'] | null>(null)

  // CONSTANTS
  const snackbarMessagesStore = useSnackbarMessagesStore()

  // REQUESTS
  const listRequest = useGetRequest<MyImportListResponse>()
  const detailsRequest = useGetRequest<ImportBatchResponse>()
  const progressRequest = useGetRequest<MyImportProgressResponse>()
  const errorsRequest = useGetRequest<MyImportErrorResponse[]>()
  const uploadRequest = usePostRequest<FormData, ImportBatchResponse>()
  const processRequest = usePostRequest<never, ProcessImportBackgroundResponse>()
  const cancelRequest = usePostRequest<never, ImportBatchResponse>()

  // METHODS
  async function fetchImports(params: MeImportsParams = {}) {
    const res = await listRequest.execute(API_URLS.me.meImports.imports, { params })
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία ανάκτησης εισαγωγών')
      return false
    }

    imports.value = [...res.items]
    totalImports.value = res.totalCount
    page.value = res.page
    pageSize.value = res.pageSize
    totalPages.value = res.totalPages
    generatedAtUtc.value = res.generatedAtUtc
    return true
  }

  async function fetchImport(request: ImportGetByIdRequest) {
    const res = await detailsRequest.execute(API_URLS.me.meImportDetails.getById(request))
    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία ανάκτησης εισαγωγής αρχείου')
      selectedImport.value = null
      return null
    }

    selectedImport.value = res
    return res
  }

  async function fetchImportProgress(importId: string) {
    const res = await progressRequest.execute(API_URLS.me.meImportDetails.progress(importId))
    if (res) updateImportInList(importId, res)
    return res
  }

  async function fetchImportErrors(importId: string) {
    return errorsRequest.execute(API_URLS.me.meImportDetails.errors(importId))
  }

  async function importPstFile(request: MeImportUploadRequest) {
    const formData = new FormData()
    formData.append('mailboxId', request.mailboxId)
    formData.append('file', request.file)

    const res = await uploadRequest.execute(API_URLS.me.meImportActions.pstUpload, {
      requestBody: formData,
    })

    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία εισαγωγής αρχείου')
      return null
    }

    snackbarMessagesStore.addSuccessMessage('Το αρχείο ανέβηκε επιτυχώς')
    return res
  }

  async function processImportInBackground(importId: string) {
    const res = await processRequest.execute(
      API_URLS.me.meImportActions.processBackground(importId),
    )

    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία έναρξης επεξεργασίας εισαγωγής')
      return false
    }

    updateImportInList(importId, { status: res.status })
    snackbarMessagesStore.addSuccessMessage('Η εισαγωγή μπήκε στην ουρά επεξεργασίας')
    return true
  }

  async function cancelImport(request: MeImportActionsCancelRequest) {
    const res = await cancelRequest.execute(API_URLS.me.meImportActions.cancel(request))

    if (!res) {
      snackbarMessagesStore.addErrorMessage('Αποτυχία ακύρωσης εισαγωγής')
      return false
    }

    updateImportInList(request.id, { status: res.status, completedAt: res.completedAt })
    if (selectedImport.value?.id === request.id) {
      selectedImport.value = res
    }

    snackbarMessagesStore.addSuccessMessage('Η εισαγωγή ακυρώθηκε')
    return true
  }

  function updateImportInList(importId: string, changes: Partial<MyImportResponse>) {
    imports.value = imports.value.map((i) => (i.id === importId ? { ...i, ...changes } : i))
  }

  function isImportPending(importId: string) {
    const item = imports.value.find((i) => i.id === importId)
    return item !== undefined && !item.isCompleted
  }

  function areImportsUnderway() {
    return imports.value.some((i) => !i.isCompleted)
  }

  function $reset() {
    imports.value = []
    selectedImport.value = null
    totalImports.value = 0
    page.value = 1
    pageSize.value = 0
    totalPages.value = 0
    generatedAtUtc.value = null
  }

  return {
    imports,
    selectedImport,
    totalImports,
    page,
    pageSize,
    totalPages,
    generatedAtUtc,
    isLoadingImports: listRequest.isLoading,
    importsError: listRequest.error,
    isLoadingImport: detailsRequest.isLoading,
    importError: detailsRequest.error,
    isLoadingProgress: progressRequest.isLoading,
    progressError: progressRequest.error,
    isLoadingErrors: errorsRequest.isLoading,
    errorsError: errorsRequest.error,
    isUploading: uploadRequest.isLoading,
    uploadError: uploadRequest.error,
    isStartingProcessing: processRequest.isLoading,
    processError: processRequest.error,
    isCancelling: cancelRequest.isLoading,
    cancelError: cancelRequest.error,
    fetchImports,
    fetchImport,
    fetchImportProgress,
    fetchImportErrors,
    importPstFile,
    processImportInBackground,
    cancelImport,
    isImportPending,
    areImportsUnderway,
    $reset,
  }
})
