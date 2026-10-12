import { ref, watch } from 'vue'
import { defineStore } from 'pinia'
import { STORAGE_KEYS } from '@/constants/storage-keys'

function readAllowScripts() {
  try {
    return sessionStorage.getItem(STORAGE_KEYS.allowEmailScripts) === 'true'
  } catch {
    return false
  }
}

export const useEmailSettingsStore = defineStore('email-settings', () => {
  // STATE
  const allowScripts = ref(readAllowScripts())

  watch(allowScripts, (value) => {
    try {
      if (value) sessionStorage.setItem(STORAGE_KEYS.allowEmailScripts, 'true')
      else sessionStorage.removeItem(STORAGE_KEYS.allowEmailScripts)
    } catch {
      // Storage unavailable (e.g. blocked); the setting then lasts until refresh
    }
  })

  function $reset() {
    allowScripts.value = false
  }

  return { allowScripts, $reset }
})
