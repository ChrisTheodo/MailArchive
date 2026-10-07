import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { usePostRequest } from '@/composables/usePostRequest'
import { useGetRequest } from '@/composables/useGetRequest'

export const useImportStore = defineStore('import', () => {
  const uploads = ref([])

  async function uploadPstFile() {}

  async function cancelUpload(importId: string) {}

  async function isImportPending(importId: string) {}

  function areUploadsUnderway() {
    return uploads.value.length > 0
  }

  return { areUploadsUnderway, uploadPstFile, cancelUpload, isImportPending }
})
