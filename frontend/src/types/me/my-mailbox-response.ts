import type { DotNetDateTime } from '../date-time'

export interface MyMailboxResponse {
  mailboxId: string
  displayName: string
  ownerUserId?: string | null
  ownerEmail?: string | null
  totalEmails: number
  emailsWithAttachments: number
  totalAttachments: number
  totalImportBatches: number
  pendingImports: number
  queuedImports: number
  runningImports: number
  completedImports: number
  completedWithErrorsImports: number
  failedImports: number
  cancelledImports: number
  totalImportErrors: number
  latestEmailReceivedAt?: DotNetDateTime | null
  latestImportStartedAt?: DotNetDateTime | null
}
