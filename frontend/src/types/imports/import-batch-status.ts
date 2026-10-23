export type ImportBatchStatus =
  'Pending' | 'Running' | 'Completed' | 'Failed' | 'CompletedWithErrors' | 'Cancelled' | 'Queued'

export const ImportBatchStatus = {
  Pending: 'Pending',
  Running: 'Running',
  Completed: 'Completed',
  Failed: 'Failed',
  CompletedWithErrors: 'CompletedWithErrors',
  Cancelled: 'Cancelled',
  Queued: 'Queued',
} as const satisfies { [K in ImportBatchStatus]: K }
