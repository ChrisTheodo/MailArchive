import type { DotNetDateTime } from '../date-time'
import type { UserRole } from '../users/user-role'
import type { Mailbox } from './mailbox'

export interface User {
  id: number
  email: string
  displayName: string
  userRole: UserRole
  isActive: boolean
  createdAt: DotNetDateTime
  updatedAt?: DotNetDateTime | null
  mailboxes: Mailbox[]
}
