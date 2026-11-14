import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { useMe } from '../../shared/api/me.ts'
import { Button } from '../../shared/ui/Button.tsx'
import { TextField } from '../../shared/ui/TextField.tsx'
import { LogoutButton } from '../auth/LogoutButton.tsx'
import { ProfileDetails } from './ProfileDetails.tsx'

/** US-04: the logged-in Player's own Profile, email included (AC-09, AC-13). Render inside RequireAuth. */
export function OwnProfilePage() {
  const { data: me } = useMe()
  const navigate = useNavigate()
  const [lookup, setLookup] = useState('')

  if (!me) {
    return null
  }

  function openProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const username = lookup.trim()
    if (username) {
      navigate(`/players/${encodeURIComponent(username)}`)
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-lg flex-col justify-center gap-6 p-6">
      <ProfileDetails
        username={me.username}
        email={me.email}
        joinedAt={me.joinedAt}
        role={me.role}
      />
      {/* Exact username only — no suggestions or partial matches (AC-12). */}
      <form onSubmit={openProfile} className="flex items-end gap-2">
        <div className="flex-1">
          <TextField
            label="Look up a player"
            name="lookup"
            autoComplete="off"
            value={lookup}
            onChange={(event) => setLookup(event.target.value)}
          />
        </div>
        <Button type="submit">Open profile</Button>
      </form>
      <div>
        <LogoutButton />
      </div>
    </main>
  )
}
