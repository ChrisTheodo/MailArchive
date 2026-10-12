import type { DotNetDateTime } from '../date-time'
import type { Attachment } from './attachment'
import type { EmailRecipient } from './email-recipient'
import type { ImportBatch } from './import-batch'
import type { Mailbox } from './mailbox'

export interface Email {
  id: string
  mailboxId: string
  mailbox: Mailbox
  importBatchId: string
  importBatch: ImportBatch
  internetMessageId?: string | null
  messageHash: string
  folderPath?: string | null
  senderMail?: string | null
  senderName?: string | null
  subject?: string | null
  bodyText?: string | null
  bodyHtml?: string | null
  sentAt?: DotNetDateTime | null
  receivedAt?: DotNetDateTime | null
  hasAttachments: boolean
  createdAt: DotNetDateTime
  recipients: EmailRecipient[]
  attachments: Attachment[]
}
