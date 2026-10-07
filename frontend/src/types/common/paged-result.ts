export interface PagedResult<T> {
  items: Array<T>
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
