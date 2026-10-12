import type { DotNetDateTime } from '../date-time'
import type { MyMailboxResponse } from './my-mailbox-response'

export interface MyMailboxListResponse {
  currentUserId: string
  currentUserEmail?: string | null
  currentUserRole?: string | null
  totalMailboxes: number
  mailboxes: ReadonlyArray<MyMailboxResponse>
  generatedAtUtc: DotNetDateTime
}
