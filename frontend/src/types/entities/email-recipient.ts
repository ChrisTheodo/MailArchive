import type { RecipientType } from '../recipient-type'
import type { Email } from './email'

export interface EmailRecipient {
  id: string
  emailId: string
  email: Email
  recipientType: RecipientType
  recipientEmail: string
  recipientName?: string | null
}
