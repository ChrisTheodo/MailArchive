import type { ApiResponse } from '@/types/common/api-response'
import { isAxiosError } from 'axios'

export function getErrorMessage(e: unknown): string {
  if (isAxiosError<ApiResponse<unknown>>(e)) {
    return e.response?.data?.error ?? e.message
  }
  if (e instanceof Error) return e.message
  return 'Unknown error'
}
