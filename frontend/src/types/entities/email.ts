import type { Mailbox } from './mailbox'

export interface Email {
  id: string
  mailboxId: string
  mailbox: Mailbox
  internetMessageId?: string | null
  folderPath?: string | null
  senderMail?: string | null
  senderName?: string | null
  subject?: string | null
}
