import type { ReactNode } from 'react'
import { Link, Outlet } from 'react-router'
import { useMe } from '../../shared/api/me.ts'

/**
 * Guards logged-in pages: a Visitor sees no profile details and is asked to log in or register
 * first (AC-10). Use as a layout route, or wrap `children`.
 */
export function RequireAuth({ children }: { children?: ReactNode }) {
  const me = useMe()

  if (me.isPending) {
    return (
      <p role="status" className="p-6 text-center text-gray-600">
        Loading…
      </p>
    )
  }

  if (me.isError) {
    return (
      <p role="alert" className="p-6 text-center text-red-800">
        Could not check your session. Please reload the page.
      </p>
    )
  }

  if (me.data === null) {
    return (
      <main className="mx-auto flex min-h-screen max-w-sm flex-col justify-center gap-4 p-6 text-center">
        <h1 className="text-2xl font-bold">Log in or register first.</h1>
        <p className="flex justify-center gap-4">
          <Link to="/login" className="text-blue-700 underline">
            Log in
          </Link>
          <Link to="/register" className="text-blue-700 underline">
            Register
          </Link>
        </p>
      </main>
    )
  }

  return children ?? <Outlet />
}
