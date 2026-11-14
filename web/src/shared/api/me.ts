import { type QueryClient, useQuery } from '@tanstack/react-query'
import { ApiError, apiFetch } from './client.ts'
import type { OwnProfile } from './types.ts'

/** The SPA's «who am I» query (openapi.yaml · getOwnProfile). */
export const meQueryKey = ['me'] as const

async function fetchMe(): Promise<OwnProfile | null> {
  try {
    return await apiFetch<OwnProfile>('/api/v1/me')
  } catch (error) {
    // 401 means a Visitor: no session, logged out, expired or revoked (AC-08, AC-10).
    if (error instanceof ApiError && error.status === 401) {
      return null
    }
    throw error
  }
}

/**
 * How long a known identity is trusted without asking `/me` again. Short, and safe: any 401 from
 * another query drops it at once (see `createQueryClient`).
 */
export const ME_STALE_TIME_MS = 30_000

/** The logged-in Player's own Profile, or `null` for a Visitor. Server errors stay errors. */
export function useMe() {
  return useQuery({ queryKey: meQueryKey, queryFn: fetchMe, staleTime: ME_STALE_TIME_MS })
}

/** Caches the identity returned by register or log in, so no extra `/me` round trip is needed. */
export function setMe(queryClient: QueryClient, profile: OwnProfile) {
  queryClient.setQueryData(meQueryKey, profile)
}

/** Forgets the cached identity after logging out (SAD §6 «Log out»). */
export function clearMe(queryClient: QueryClient) {
  queryClient.setQueryData(meQueryKey, null)
}
