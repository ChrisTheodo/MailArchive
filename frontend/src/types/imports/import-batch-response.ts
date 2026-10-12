import type { DotNetDateTime } from '../date-time'

export interface ImportBatchResponse {
  id: string
  pstFilename: string
  mailboxId: string
  mailboxDisplayName?: string | null
  mailboxOwnerEmail?: string | null
  status: string
  startedAt: DotNetDateTime
  completedAt?: DotNetDateTime | null
  totalMessages: number
  importedMessages: number
  failedMessages: number
}
