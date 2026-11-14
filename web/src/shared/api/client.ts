import type { FieldErrors } from './types.ts'

/** CSRF barrier (accounts ADR-0005): sent on every request; the server requires it on unsafe ones. */
export const CLIENT_HEADER = 'X-Rochambeau-Client'

/** A failed API call, carrying the RFC 9457 ProblemDetails the server sent. */
export class ApiError extends Error {
  readonly status: number
  readonly errorCode: string
  readonly title: string
  readonly errors?: FieldErrors
  readonly lockoutEnd?: Date

  constructor(init: {
    status: number
    errorCode: string
    title: string
    errors?: FieldErrors
    lockoutEnd?: Date
  }) {
    super(init.title)
    this.name = 'ApiError'
    this.status = init.status
    this.errorCode = init.errorCode
    this.title = init.title
    this.errors = init.errors
    this.lockoutEnd = init.lockoutEnd
  }
}

export interface ApiRequest {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
}

/**
 * Same-origin JSON fetch for `/api`: sends the session cookie and the client header, returns the
 * parsed body (or `undefined` for 204), and throws {@link ApiError} for any non-2xx response.
 */
export async function apiFetch<T = unknown>(path: string, request: ApiRequest = {}): Promise<T> {
  const headers = new Headers({ Accept: 'application/json', [CLIENT_HEADER]: 'web' })
  if (request.body !== undefined) {
    headers.set('Content-Type', 'application/json')
  }

  const response = await fetch(path, {
    method: request.method ?? 'GET',
    headers,
    credentials: 'same-origin',
    body: request.body === undefined ? undefined : JSON.stringify(request.body),
  })

  if (!response.ok) {
    throw await toApiError(response)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

interface ProblemBody {
  title?: string
  errorCode?: string
  errors?: FieldErrors
  lockoutEnd?: string
}

async function toApiError(response: Response): Promise<ApiError> {
  const contentType = response.headers.get('Content-Type') ?? ''
  let body: ProblemBody = {}
  if (contentType.includes('json')) {
    try {
      body = (await response.json()) as ProblemBody
    } catch {
      body = {}
    }
  }

  return new ApiError({
    status: response.status,
    errorCode: body.errorCode ?? 'unknown',
    title: body.title ?? (response.statusText || 'Something went wrong. Please try again.'),
    errors: body.errors,
    lockoutEnd: body.lockoutEnd ? new Date(body.lockoutEnd) : undefined,
  })
}
