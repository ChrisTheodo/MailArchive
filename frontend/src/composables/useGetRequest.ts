import type { ApiResponse } from '@/types/common/api-response'
import { ref, shallowRef } from 'vue'
import api from './axios-api'
import { type AxiosRequestConfig } from 'axios'
import { getErrorMessage } from './useGetErrorMessage'

export function useGetRequest<T>(defaultConfig?: AxiosRequestConfig) {
  const data = shallowRef<T | null>(null)
  const error = ref<string | null>(null)
  const isLoading = ref(false)

  const execute = async (url: string, config?: AxiosRequestConfig) => {
    isLoading.value = true
    error.value = null

    try {
      const res = await api.get<ApiResponse<T>>(url, { ...defaultConfig, ...config })
      const body = res.data

      if (body.success) {
        data.value = body.data
        return body.data
      }

      data.value = null
      error.value = body.error ?? 'Unknown error'
      return null
    } catch (e) {
      data.value = null
      error.value = getErrorMessage(e)
      return null
    } finally {
      isLoading.value = false
    }
  }

  return { execute, data, error, isLoading }
}
