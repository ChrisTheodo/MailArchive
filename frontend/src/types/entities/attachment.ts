import type { DotNetDateTime } from '../date-time'
import type { Email } from './email'

export interface Attachment {
  id: string
  emailId: string
  email: Email
  fileName: string
  contentType?: string | null
  sizeBytes: number
  storagePath: string
  contentHash: string
  createdAt: DotNetDateTime
}
