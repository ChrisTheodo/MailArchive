import type { UserResponse } from '@/types/users/user-response'
import type { DotNetDateTime } from '@/types/date-time'

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAtUtc: DotNetDateTime
  refreshToken: string
  refreshTokenExpiresAtUtc: DotNetDateTime
  user: UserResponse
}
