import type { DotNetDateTime } from '../date-time'
import type { MailboxSourceType } from '../mailboxes/mailbox-source-type'
import type { Email } from './email'
import type { User } from './user'

export interface Mailbox {
  id: string
  ownerUser: User
  ownerUserId: string
  displayName: string
  mailboxSource: MailboxSourceType
  isAssigned: boolean
  createdAt: DotNetDateTime
  emails: Email[]
}
