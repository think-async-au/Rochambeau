import { useQuery } from '@tanstack/react-query'
import { ApiError, apiFetch } from '../../shared/api/client.ts'
import type { PublicProfile } from '../../shared/api/types.ts'

/** Another Player's Profile by exact Username (openapi.yaml · getPublicProfile). */
export function usePublicProfile(username: string) {
  return useQuery({
    queryKey: ['profiles', username],
    queryFn: () => apiFetch<PublicProfile>(`/api/v1/profiles/${encodeURIComponent(username)}`),
    // A 4xx (e.g. 404 no such player) will not change on retry.
    retry: (failures, error) => !(error instanceof ApiError && error.status < 500) && failures < 3,
  })
}
