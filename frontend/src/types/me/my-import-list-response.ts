import type { PagedResult } from '../common/paged-result'
import type { DotNetDateTime } from '../date-time'
import type { MyImportResponse } from './my-import-response'

export interface MyImportListResponse extends Omit<PagedResult<MyImportResponse>, 'items'> {
  readonly items: ReadonlyArray<MyImportResponse>
  currentUserId: string
  currentUserEmail?: string | null
  currentUserRole?: string | null
  generatedAtUtc: DotNetDateTime
}
