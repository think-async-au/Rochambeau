import { QueryCache, QueryClient, type QueryClientConfig } from '@tanstack/react-query'
import { ApiError } from './client.ts'
import { clearMe } from './me.ts'

/**
 * The SPA's QueryClient. A 401 from any query means the session ended — logged out elsewhere,
 * expired, or revoked by a role change (AC-08, AC-13b) — so the cached identity is dropped and
 * every logged-in page falls back to «Log in or register first.» (SAD §6 critical flow 2).
 */
export function createQueryClient(config: QueryClientConfig = {}): QueryClient {
  const queryClient: QueryClient = new QueryClient({
    ...config,
    queryCache: new QueryCache({
      onError: (error) => {
        if (error instanceof ApiError && error.status === 401) {
          clearMe(queryClient)
        }
      },
    }),
  })
  return queryClient
}
