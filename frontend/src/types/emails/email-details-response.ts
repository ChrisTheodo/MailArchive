import type { DotNetDateTime } from '../date-time'
import type { EmailAttachmentResponse } from './email-attachment-response'
import type { EmailRecipientResponse } from './email-recipient-response'

export interface EmailDetailsResponse {
  id: string
  mailboxId: string
  mailboxDisplayName?: string | null
  importBatchId: string
  internetMessageId?: string | null
  messageHash: string
  folderPath?: string | null
  senderEmail?: string | null
  senderName?: string | null
  subject?: string | null
  bodyText?: string | null
  bodyHtml?: string | null
  sentAt?: DotNetDateTime | null
  receivedAt?: DotNetDateTime | null
  hasAttachments: boolean
  createdAt: DotNetDateTime
  readonly recipients: EmailRecipientResponse[]
  readonly attachments: EmailAttachmentResponse[]
}
