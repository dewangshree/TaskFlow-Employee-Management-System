import axios, { AxiosError } from 'axios'
import type { ProblemDetails } from './types'

/**
 * Same-origin client. Auth is an HttpOnly cookie set by ASP.NET Core Identity.
 * Axios reads the XSRF-TOKEN cookie and sends it as X-XSRF-TOKEN on every
 * same-origin request, which the API validates on state-changing calls.
 */
export const api = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
  xsrfCookieName: 'XSRF-TOKEN',
  xsrfHeaderName: 'X-XSRF-TOKEN',
  paramsSerializer: { indexes: null },
})

export const UNAUTHORIZED_EVENT = 'taskflow:unauthorized'

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const url = error.config?.url ?? ''
    if (error.response?.status === 401 && !url.startsWith('/auth/')) {
      window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
    }
    return Promise.reject(error)
  },
)

export class ApiError extends Error {
  status: number
  problem: ProblemDetails
  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? 'Request failed')
    this.status = status
    this.problem = problem
  }
  get isConflict() { return this.status === 409 }
  get fieldErrors() { return this.problem.errors ?? {} }
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  if (axios.isAxiosError(error)) {
    const status = error.response?.status ?? 0
    const data = (error.response?.data ?? {}) as ProblemDetails
    if (status === 0) return new ApiError(0, { title: 'Network error', detail: 'Unable to reach the server. Check your connection.' })
    return new ApiError(status, typeof data === 'object' ? data : { detail: String(data) })
  }
  return new ApiError(0, { detail: error instanceof Error ? error.message : 'Unexpected error' })
}

/** Drops empty filter values so URLs and cache keys stay clean. */
export function cleanParams<T extends object>(params: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  ) as Partial<T>
}
