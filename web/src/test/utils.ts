import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { createElement, type ReactElement } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router'

/** Renders routes inside a fresh QueryClient (no retries) and a MemoryRouter at `route`. */
export function renderRoutes(routes: Record<string, ReactElement>, route: string) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const view = render(
    createElement(
      QueryClientProvider,
      { client: queryClient },
      createElement(
        MemoryRouter,
        { initialEntries: [route] },
        createElement(
          Routes,
          null,
          Object.entries(routes).map(([path, element]) =>
            createElement(Route, { key: path, path, element }),
          ),
        ),
      ),
    ),
  )
  return { queryClient, ...view }
}

type Handler = (url: string, init: RequestInit | undefined) => Response | Promise<Response>

/** Stubs `fetch` with a handler that gets a fresh call per request; returns the mock for assertions. */
export function mockFetch(handler: Handler) {
  const fetchMock = vi.fn<typeof fetch>((input, init) =>
    Promise.resolve(handler(String(input), init)),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

export function problem(status: number, body: Record<string, unknown>) {
  return new Response(JSON.stringify({ status, ...body }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

export function requestBody(init: RequestInit | undefined): unknown {
  return JSON.parse(String(init?.body))
}
