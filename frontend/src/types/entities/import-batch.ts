import type { DotNetDateTime } from '../date-time'
import type { ImportBatchStatus } from '../imports/import-batch-status'
import type { Email } from './email'
import type { ImportError } from './import-error'
import type { Mailbox } from './mailbox'

export interface ImportBatch {
  id: string
  pstFilename: string
  pstHash: string
  pstStoragePath?: string | null
  mailboxId: string
  mailbox: Mailbox
  status: ImportBatchStatus
  startedAt: DotNetDateTime
  completedAt?: DotNetDateTime | null
  totalMessages: number
  importedMessages: number
  failedMessages: number
  emails: Email[]
  importErrors: ImportError[]
}
