import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { clearMe, meQueryKey, setMe, useMe } from './me.ts'
import type { OwnProfile } from './types.ts'

const rocky: OwnProfile = {
  username: 'Rocky',
  email: 'user-1@example.test',
  joinedAt: '2026-11-14T10:00:00Z',
  role: 'player',
}

function setup() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  )
  return { queryClient, wrapper }
}

describe('useMe', () => {
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchMock.mockReset()
    vi.stubGlobal('fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('resolves to the own profile for a logged-in Player', async () => {
    fetchMock.mockResolvedValue(Response.json(rocky))
    const { wrapper } = setup()

    const { result } = renderHook(() => useMe(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(rocky)
    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/me')
  })

  it('uses the identity cached by setMe without another /me round trip', async () => {
    const { queryClient, wrapper } = setup()
    setMe(queryClient, rocky)

    const { result } = renderHook(() => useMe(), { wrapper })

    expect(result.current.data).toEqual(rocky)
    await new Promise((resolve) => setTimeout(resolve, 0))
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('resolves to null for a Visitor (401), not to an error', async () => {
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          status: 401,
          title: 'Log in or register first.',
          errorCode: 'identity.not_authenticated',
        }),
        { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    )
    const { wrapper } = setup()

    const { result } = renderHook(() => useMe(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toBeNull()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('is an error state on a server error, not a logged-out state', async () => {
    fetchMock.mockResolvedValue(new Response('boom', { status: 500 }))
    const { wrapper } = setup()

    const { result } = renderHook(() => useMe(), { wrapper })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(result.current.data).toBeUndefined()
  })

  it('can be set after logging in and cleared after logging out', async () => {
    fetchMock.mockResolvedValue(Response.json(rocky))
    const { queryClient, wrapper } = setup()
    const { result } = renderHook(() => useMe(), { wrapper })
    // Read `data` first, as a component would during render (TanStack re-renders on tracked props).
    await waitFor(() => expect(result.current.data).toEqual(rocky))

    act(() => clearMe(queryClient))
    await waitFor(() => expect(result.current.data).toBeNull())

    act(() => setMe(queryClient, { ...rocky, role: 'administrator' }))
    await waitFor(() => expect(result.current.data?.role).toBe('administrator'))
    expect(queryClient.getQueryData(meQueryKey)).toEqual({ ...rocky, role: 'administrator' })
  })
})
