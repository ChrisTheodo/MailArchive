import type { DeleteEmailRequest } from '@/types/emails/delete-email-request'
import type { DeleteMailboxRequest } from '@/types/emails/delete-mailbox-request'
import type { ImportGetByIdRequest } from '@/types/imports/import-get-by-id-request'
import type { MailboxGetByIdRequest } from '@/types/mailboxes/mailbox-get-by-id-request'
import type { MeImportActionsCancelRequest } from '@/types/me/me-import-actions-cancel-request'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL.replace(/\/+$/, '')

export const API_URLS = {
  admin: {
    auditLogs: `${API_BASE_URL}/api/admin/audit-logs`,
    adminDashboard: `${API_BASE_URL}/api/admin/dashboard/summary`,
    adminDashboardMailboxUsage: `${API_BASE_URL}/api/admin/dashboard/mailbox-usage`,
    adminDashboardRecentActivity: `${API_BASE_URL}/api/admin/dashboard/recent-activity`,
    adminDashboardStorageUsage: `${API_BASE_URL}/api/admin/dashboard/storage-usage`,
  },
  attachments: {
    download: (id: string) => `${API_BASE_URL}/api/attachments/${id}/download`,
  },
  auth: {
    login: `${API_BASE_URL}/api/auth/login`,
    refresh: `${API_BASE_URL}/api/refresh`,
    refreshRevoke: `${API_BASE_URL}/api/refresh/revoke`,
    logout: `${API_BASE_URL}/api/auth/logout`,
  },
  emails: {
    list: `${API_BASE_URL}/api/emails`,
    getById: (id: string) => `${API_BASE_URL}/api/emails/${id}`,
    attachments: (id: string) => `${API_BASE_URL}/api/emails/${id}/attachments`,
    delete: (request: DeleteEmailRequest) => `${API_BASE_URL}/api/emails/${request.id}/delete`,
  },
  mailboxes: {
    list: `${API_BASE_URL}/api/mailboxes`,
    post: `${API_BASE_URL}/api/mailboxes`,
    getById: (request: MailboxGetByIdRequest) => `${API_BASE_URL}/api/mailboxes/${request.id}`,
    putById: (id: string) => `${API_BASE_URL}/api/mailboxes/${id}`,
    delete: (request: DeleteMailboxRequest) => `${API_BASE_URL}/api/mailboxes/${request.id}/delete`,
  },
  users: {
    list: `${API_BASE_URL}/api/users`,
    post: `${API_BASE_URL}/api/users`,
    getById: (id: string) => `${API_BASE_URL}/api/users/${id}`,
    putById: (id: string) => `${API_BASE_URL}/api/users/${id}`,
  },
  me: {
    me: `${API_BASE_URL}/api/me`,
    meDashboard: `${API_BASE_URL}/api/me/dashboard/summary`,
    meDashboardRecentActivity: `${API_BASE_URL}/api/me/dashboard/recent-activity`,
    meImportActions: {
      pstUpload: `${API_BASE_URL}/api/me/imports/pst/upload`,
      processBackground: (id: string) => `${API_BASE_URL}/api/me/imports/${id}/process/background`,
      cancel: (request: MeImportActionsCancelRequest) =>
        `${API_BASE_URL}/api/me/imports/${request.id}/cancel`,
    },
    meImportDetails: {
      getById: (request: ImportGetByIdRequest) => `${API_BASE_URL}/api/me/imports/${request.id}`,
      progress: (id: string) => `${API_BASE_URL}/api/me/imports/${id}/progress`,
      errors: (id: string) => `${API_BASE_URL}/api/me/imports/${id}/errors`,
    },
    meImports: {
      imports: `${API_BASE_URL}/api/me/imports`,
    },
    meMailboxes: {
      mailboxes: `${API_BASE_URL}/api/me/mailboxes`,
      post: `${API_BASE_URL}/api/me/mailboxes`,
    },
  },
} as const
