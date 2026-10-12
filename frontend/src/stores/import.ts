import { defineStore } from 'pinia'
import { ref } from 'vue'
import { usePostRequest } from '@/composables/usePostRequest'
import { useGetRequest } from '@/composables/useGetRequest'
import { API_URLS } from '@/constants/api-urls'
import type { MyImportResponse } from '@/types/me/my-import-response'
import type { MyImportListResponse } from '@/types/me/my-import-list-response'
import type { MeImportsParams } from '@/types/me/me-imports-params'
import type { MeImportActionsCancelRequest } from '@/types/me/me-import-actions-cancel-request'
import type { MeImportActionsCancelResponse } from '@/types/me/me-import-actions-cancel-response'
import type { ImportGetByIdRequest } from '@/types/imports/import-get-by-id-request'
import type { ImportBatchResponse } from '@/types/imports/import-batch-response'
import type { MeImportUploadRequest } from '@/types/me/me-import-upload-request'
import { useSnackbarMessagesStore } from '@/stores/snackbar-messages'

export const useImportStore = defineStore('import', () => {
  const imports = ref<MyImportResponse[]>([])
  const { addSuccessMessage, addErrorMessage, addLoaderMessage } = useSnackbarMessagesStore()

  async function getAllImports(params: MeImportsParams) {
    const { execute, isLoading } = useGetRequest<MyImportListResponse>(
      API_URLS.me.meImports.imports,
      { params },
    )
    const res = await execute()

    if (!res) return null
  }

  async function getImport(request: ImportGetByIdRequest) {
    const { execute, isLoading } = useGetRequest<ImportBatchResponse>(
      API_URLS.me.meImportDetails.getById(request),
    )

    if (isLoading) addLoaderMessage('Ανάκτηση εισαγωγής αρχείου σε εξέλιξη...')

    const res = await execute()
    if (!res) addErrorMessage('Αποτυχία ανάκτησης εισαγωγής αρχείου')
    console.log(
      `Import: ${res?.id} with mailboxId: ${res?.mailboxId} and pstFilename: ${res?.pstFilename}`,
    )
    addSuccessMessage('Επιτυχία ανάκτησης εισαγωγής αρχείου')
  }

  async function importPstFile() {
    const { execute, isLoading } = usePostRequest<MeImportUploadRequest, ImportBatchResponse>(
      API_URLS.me.meImportActions.pstUpload,
    )
    const res = await execute()
  }

  async function cancelImport(request: MeImportActionsCancelRequest) {
    const { execute, isLoading } = usePostRequest<
      MeImportActionsCancelRequest,
      MeImportActionsCancelResponse
    >(API_URLS.me.meImportActions.cancel(request))

    const res = await execute()
    if (!res) console.error(`Import cancel with id: ${request.id} failed`)

    console.log(`Successful cancel of import of importId ${request.id}`)
  }

  async function isImportPending(importId: string) {}

  function areImportsUnderway() {
    return imports.value.length > 0
  }

  return {
    areImportsUnderway,
    importPstFile,
    cancelImport,
    isImportPending,
    getAllImports,
    getImport,
  }
})
