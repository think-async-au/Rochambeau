import { Link, useParams } from 'react-router'
import { ApiError } from '../../shared/api/client.ts'
import { ProfileDetails } from './ProfileDetails.tsx'
import { usePublicProfile } from './usePublicProfile.ts'

/** US-05: another Player's Profile by exact Username — never an email (AC-11, AC-12). Render inside RequireAuth. */
export function PublicProfilePage() {
  const { username = '' } = useParams()
  const profile = usePublicProfile(username)

  return (
    <main className="mx-auto flex min-h-screen max-w-lg flex-col justify-center gap-6 p-6">
      {profile.isPending && (
        <p role="status" className="text-center text-gray-600">
          Loading…
        </p>
      )}
      {profile.isError && (
        <p role="alert" className="text-center text-gray-800">
          {profile.error instanceof ApiError && profile.error.status === 404
            ? profile.error.title
            : 'Could not load this profile. Please try again.'}
        </p>
      )}
      {profile.isSuccess && (
        // Pass only the public fields, so nothing else the server sent can render.
        <ProfileDetails
          username={profile.data.username}
          joinedAt={profile.data.joinedAt}
          role={profile.data.role}
        />
      )}
      <Link to="/profile" className="text-blue-700 underline">
        Back to my profile
      </Link>
    </main>
  )
}
