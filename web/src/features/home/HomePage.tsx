import { Link } from 'react-router'
import { useMe } from '../../shared/api/me.ts'

const linkClass = 'text-blue-700 underline'

/**
 * The first-visit page, and where log-out lands: a Visitor gets the way in (spec §2 «one short,
 * self-explanatory flow», §7 first visit → Profile), a logged-in Player gets their Profile.
 */
export function HomePage() {
  const me = useMe()

  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col items-center justify-center gap-2 p-6">
      <h1 className="text-4xl font-bold">Rochambeau</h1>
      <p className="text-gray-600">Rock · Paper · Scissors</p>
      {me.isSuccess && me.data !== null && (
        <nav className="mt-4">
          <Link to="/profile" className={linkClass}>
            My profile
          </Link>
        </nav>
      )}
      {(me.isError || (me.isSuccess && me.data === null)) && (
        <nav className="mt-4 flex gap-4">
          <Link to="/register" className={linkClass}>
            Create account
          </Link>
          <Link to="/login" className={linkClass}>
            Log in
          </Link>
        </nav>
      )}
    </main>
  )
}
