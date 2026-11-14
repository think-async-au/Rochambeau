import { ApiError, apiFetch } from './client.ts'

function problem(status: number, body: object) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('apiFetch', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('always sends the client header and same-origin credentials, and sends JSON', async () => {
    fetchMock.mockImplementation(() => Promise.resolve(Response.json({ ok: true })))

    const result = await apiFetch<{ ok: boolean }>('/api/v1/session', {
      method: 'POST',
      body: { email: 'user@example.test' },
    })
    await apiFetch('/api/v1/me')

    expect(result).toEqual({ ok: true })
    for (const [, init] of fetchMock.mock.calls) {
      const headers = new Headers(init?.headers)
      expect(headers.get('X-Rochambeau-Client')).toBe('web')
      expect(init?.credentials).toBe('same-origin')
    }
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/v1/session')
    expect(init?.method).toBe('POST')
    expect(new Headers(init?.headers).get('Content-Type')).toBe('application/json')
    expect(init?.body).toBe(JSON.stringify({ email: 'user@example.test' }))
  })

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(apiFetch('/api/v1/session', { method: 'DELETE' })).resolves.toBeUndefined()
  })

  it('throws an ApiError carrying the ProblemDetails fields', async () => {
    fetchMock.mockResolvedValue(
      problem(409, {
        type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
        title: 'This username is already taken.',
        status: 409,
        errorCode: 'identity.username_taken',
      }),
    )

    const error = await apiFetch('/api/v1/accounts', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 409,
      errorCode: 'identity.username_taken',
      title: 'This username is already taken.',
    })
  })

  it('keeps the field errors of a validation problem as-is', async () => {
    const errors = {
      username: [
        'The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen.',
      ],
      password: ['The password must be at least 8 characters.'],
    }
    fetchMock.mockResolvedValue(
      problem(400, {
        title: 'One or more fields are invalid.',
        status: 400,
        errorCode: 'identity.validation_failed',
        errors,
      }),
    )

    const error = (await apiFetch('/api/v1/accounts', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )) as ApiError

    expect(error.errors).toEqual(errors)
  })

  it('parses lockoutEnd as a date', async () => {
    fetchMock.mockResolvedValue(
      problem(423, {
        title: 'Login is temporarily blocked. Try again after the time shown.',
        status: 423,
        errorCode: 'identity.locked_out',
        lockoutEnd: '2026-11-14T10:15:00Z',
      }),
    )

    const error = (await apiFetch('/api/v1/session', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )) as ApiError

    expect(error.lockoutEnd).toEqual(new Date('2026-11-14T10:15:00Z'))
  })

  it('turns a non-JSON failure into an ApiError with the status', async () => {
    fetchMock.mockResolvedValue(
      new Response('Bad gateway', { status: 502, statusText: 'Bad Gateway' }),
    )

    const error = (await apiFetch('/api/v1/me').catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(502)
    expect(error.errorCode).toBe('unknown')
  })
})
