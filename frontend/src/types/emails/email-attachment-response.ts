export interface EmailAttachmentResponse {
  id: string
  fileName: string
  contentType?: string | null
  sizeBytes: number
  storagePath: string
  contentHash: string
}
