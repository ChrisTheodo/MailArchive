/** ISO 8601 date-time string as serialized by .NET (System.Text.Json / Newtonsoft),
 *  e.g. "2026-10-05T11:12:32.980243Z" */
export type DotNetDateTime = string & { readonly __brand: 'DotNetDateTime' }

// yyyy-MM-ddTHH:mm:ss[.fffffff][Z | ±HH:mm]
const DOTNET_DATETIME_RE = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})?$/

export function isDotNetDateTime(value: unknown): value is DotNetDateTime {
  return (
    typeof value === 'string' && DOTNET_DATETIME_RE.test(value) && !Number.isNaN(Date.parse(value))
  )
}

export function toDate(value: DotNetDateTime): Date {
  return new Date(value)
}
