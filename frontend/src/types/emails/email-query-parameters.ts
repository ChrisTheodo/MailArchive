import type { DotNetDateTime } from '@/types/date-time'

export interface EmailQueryParameters {
  page: number
  pageSize: number
  search?: string | null
  mailboxId?: string | null
  importBatchId?: string | null
  fromDate?: DotNetDateTime | null
  toDate?: DotNetDateTime | null
  sender?: string | null
  recipient?: string | null
  subject?: string | null
  folder?: string | null
  hasAttachments?: boolean | null
  attachmentFileName?: string | null
  sortBy?: string | null
  sortDescending: boolean
}
