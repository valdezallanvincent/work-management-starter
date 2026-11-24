export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type SortDirection = 'Ascending' | 'Descending';

export interface PageQuery {
  page: number;
  pageSize: number;
  sortBy?: string;
  sortDirection?: SortDirection;
  search?: string;
}

/** RFC 9457 problem details as returned by the API, including the stable `code` extension. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export type UserRole = 'Member' | 'Manager' | 'Admin';
