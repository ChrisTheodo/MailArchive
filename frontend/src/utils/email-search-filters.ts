import type { LocationQuery } from 'vue-router'
import type { EmailQueryParameters } from '@/types/emails/email-query-parameters'
import type { DotNetDateTime } from '@/types/date-time'

// Required needed as code checks only for null
export type EmailSearchFilters = Required<
  Omit<
    EmailQueryParameters,
    | 'sortDescending'
    | 'page'
    | 'pageSize'
    | 'search'
    | 'importBatchId'
    | 'folder'
    | 'sortBy'
    | 'fromDate'
    | 'toDate'
  >
> & {
  fromDate: string | null
  toDate: string | null
}

export type EmailSearchFilterKey = keyof EmailSearchFilters

export const EMPTY_SEARCH_FILTERS: Readonly<EmailSearchFilters> = {
  sender: null,
  recipient: null,
  subject: null,
  mailboxId: null,
  fromDate: null,
  toDate: null,
  hasAttachments: null,
  attachmentFileName: null,
}

const SEARCH_FILTER_KEYS = Object.keys(EMPTY_SEARCH_FILTERS) as EmailSearchFilterKey[]
const DAY_RE = /^\d{4}-\d{2}-\d{2}$/

export function filtersFromQuery(query: LocationQuery): EmailSearchFilters {
  const text = (key: EmailSearchFilterKey) => {
    const value = query[key]
    return typeof value === 'string' && value.trim() ? value.trim() : null
  }
  const day = (key: EmailSearchFilterKey) => {
    const value = text(key)
    return value && DAY_RE.test(value) ? value : null
  }
  const attachments = text('hasAttachments')

  return {
    sender: text('sender'),
    recipient: text('recipient'),
    subject: text('subject'),
    mailboxId: text('mailboxId'),
    fromDate: day('fromDate'),
    toDate: day('toDate'),
    hasAttachments: attachments === 'true' ? true : attachments === 'false' ? false : null,
    attachmentFileName: text('attachmentFileName'),
  }
}

/* Every filter key is always present so that merging into route.query also removes cleared filters */
export function filtersToQuery(filters: EmailSearchFilters) {
  const query = {} as Record<EmailSearchFilterKey, string | undefined>
  for (const key of SEARCH_FILTER_KEYS) {
    const value = filters[key]
    const text = value === null ? '' : String(value).trim()
    query[key] = text || undefined
  }
  return query
}

export function countActiveFilters(filters: EmailSearchFilters) {
  return SEARCH_FILTER_KEYS.filter((key) => filters[key] !== null && filters[key] !== '').length
}

export function filtersToApiParams(filters: EmailSearchFilters): Partial<EmailQueryParameters> {
  return {
    ...filters,
    fromDate: filters.fromDate ? toApiDateTime(`${filters.fromDate}T00:00:00`) : null,
    toDate: filters.toDate ? toApiDateTime(`${filters.toDate}T23:59:59.999`) : null,
  }
}

function toApiDateTime(localDateTime: string) {
  return new Date(localDateTime).toISOString() as DotNetDateTime
}
