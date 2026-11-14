import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { ApiError, apiFetch } from '../../shared/api/client.ts'
import { clearMe } from '../../shared/api/me.ts'
import { Button } from '../../shared/ui/Button.tsx'
import { FormError } from '../../shared/ui/FormError.tsx'

/**
 * US-03: ends this browser's session only, forgets the cached identity, shows the Visitor view (AC-07).
 * The Visitor view appears only once the server has ended the session (204), or says there was none
 * (401): the HttpOnly cookie cannot be removed here, so showing «logged out» while it is still valid
 * would leave the Player's session to the next person using this browser.
 */
export function LogoutButton() {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  const forget = () => {
    clearMe(queryClient)
    navigate('/')
  }

  const logOut = useMutation({
    mutationFn: () => apiFetch('/api/v1/session', { method: 'DELETE' }),
    onSuccess: forget,
    onError: (error) => {
      if (error instanceof ApiError && error.status === 401) {
        forget()
      }
    },
  })

  const failed =
    logOut.isError && !(logOut.error instanceof ApiError && logOut.error.status === 401)

  return (
    <div className="flex flex-col gap-2">
      <FormError message={failed ? 'Could not log out. Please try again.' : undefined} />
      <Button onClick={() => logOut.mutate()} disabled={logOut.isPending}>
        Log out
      </Button>
    </div>
  )
}
