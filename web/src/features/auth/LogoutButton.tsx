import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { apiFetch } from '../../shared/api/client.ts'
import { clearMe } from '../../shared/api/me.ts'
import { Button } from '../../shared/ui/Button.tsx'

/** US-03: ends this browser's session only, forgets the cached identity, shows the Visitor view (AC-07). */
export function LogoutButton() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const logOut = useMutation({
    mutationFn: () => apiFetch('/api/v1/session', { method: 'DELETE' }),
    // Whatever the server said, this browser must stop acting as the Player.
    onSettled: () => {
      clearMe(queryClient)
      navigate('/')
    },
  })

  return (
    <Button onClick={() => logOut.mutate()} disabled={logOut.isPending}>
      Log out
    </Button>
  )
}
