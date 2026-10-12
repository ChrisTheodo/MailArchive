import type { DotNetDateTime } from '../date-time'

export interface EmailListResponse {
  id: string
  mailboxId: string
  mailboxDisplayName: string
  internetMessageId?: string | null
  folderPath: string
  senderEmail: string
  senderName?: string | null
  subject?: string | null
  sentAt?: DotNetDateTime | null
  receivedAt?: DotNetDateTime | null
  hasAttachments: boolean
  readonly recipientEmails: string[]
  readonly attachmentFileNames: string[]
  searchSnippet?: string | null
}
