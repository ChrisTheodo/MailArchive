import type { DotNetDateTime } from '../date-time'
import type { ImportBatch } from './import-batch'

export interface ImportError {
  id: string
  importBatchId: string
  importBatch: ImportBatch
  /** Defaults to empty string */
  message: string
  createdAt: DotNetDateTime
}
