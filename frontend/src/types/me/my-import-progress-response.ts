import type { DotNetDateTime } from '../date-time'

export interface MyImportProgressResponse {
  id: string
  pstFilename: string
  mailboxId: string
  mailboxDisplayName?: string | null
  status: string
  progressPercent: number
  totalMessages: number
  importedMessages: number
  failedMessages: number
  errorCount: number
  isCompleted: boolean
  hasErrors: boolean
  startedAt: DotNetDateTime
  completedAt?: DotNetDateTime | null
}
