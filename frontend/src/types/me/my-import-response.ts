import type { DotNetDateTime } from '../date-time'

export interface MyImportResponse {
  id: string
  pstFilename: string
  mailboxDisplayName?: string | null
  status: string
  startedAt: DotNetDateTime
  completedAt?: DotNetDateTime | null
  totalMessages: number
  importedMessages: number
  failedMessages: number
  errorCount: number
  progressPercent: number
  isCompleted: boolean
  hasErrors: boolean
}
