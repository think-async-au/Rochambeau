import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Link, Route, Routes } from 'react-router'
import { meQueryKey } from '../../shared/api/me.ts'
import type { OwnProfile } from '../../shared/api/types.ts'
import { mockFetch, problem, renderRoutes } from '../../test/utils.ts'
import { LogoutButton } from './LogoutButton.tsx'
import { RequireAuth } from './RequireAuth.tsx'

const rocky: OwnProfile = {
  username: 'Rocky',
  email: 'user-1@example.test',
  joinedAt: '2026-11-14T10:00:00Z',
  role: 'player',
}

const notAuthenticated = () =>
  problem(401, { title: 'Log in or register first.', errorCode: 'identity.not_authenticated' })

function Guarded() {
  return (
    <Routes>
      <Route element={<RequireAuth />}>
        <Route
          index
          element={
            <div>
              <p>Secret profile</p>
              <LogoutButton />
            </div>
          }
        />
      </Route>
    </Routes>
  )
}

function renderGuarded() {
  return renderRoutes({ '/profile/*': <Guarded />, '/': <p>Visitor home</p> }, '/profile')
}

describe('RequireAuth', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows a loading state while it checks the session', async () => {
    mockFetch(() => new Promise<Response>(() => {}))
    renderGuarded()

    expect(await screen.findByRole('status')).toHaveTextContent(/loading/i)
    expect(screen.queryByText('Secret profile')).not.toBeInTheDocument()
  })

  it('asks a Visitor to log in or register first and shows no profile (AC-10)', async () => {
    mockFetch(notAuthenticated)
    renderGuarded()

    expect(await screen.findByText('Log in or register first.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login')
    expect(screen.getByRole('link', { name: 'Register' })).toHaveAttribute('href', '/register')
    expect(screen.queryByText('Secret profile')).not.toBeInTheDocument()
  })

  it('shows the page to a logged-in Player', async () => {
    mockFetch(() => Response.json(rocky))
    renderGuarded()

    expect(await screen.findByText('Secret profile')).toBeInTheDocument()
  })

  it('shows an error, not the Visitor view, when the session check fails', async () => {
    mockFetch(() => new Response('boom', { status: 500 }))
    renderGuarded()

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.queryByText('Log in or register first.')).not.toBeInTheDocument()
  })
})

describe('LogoutButton', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('ends the session, forgets the identity and shows the portal as a Visitor (AC-07)', async () => {
    const fetchMock = mockFetch((url, init) =>
      url === '/api/v1/session' && init?.method === 'DELETE'
        ? new Response(null, { status: 204 })
        : Response.json(rocky),
    )
    const { queryClient } = renderGuarded()
    expect(await screen.findByText('Secret profile')).toBeInTheDocument()

    await userEvent.setup().click(screen.getByRole('button', { name: 'Log out' }))

    expect(await screen.findByText('Visitor home')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/v1/session',
      expect.objectContaining({ method: 'DELETE' }),
    )
    expect(queryClient.getQueryData(meQueryKey)).toBeNull()
  })

  it('after logging out, going back to the guarded page asks to log in again', async () => {
    let loggedIn = true
    mockFetch((url, init) => {
      if (url === '/api/v1/session' && init?.method === 'DELETE') {
        loggedIn = false
        return new Response(null, { status: 204 })
      }
      return loggedIn ? Response.json(rocky) : notAuthenticated()
    })
    renderRoutes(
      { '/profile/*': <Guarded />, '/': <Link to="/profile">Back to profile</Link> },
      '/profile',
    )
    const user = userEvent.setup()
    expect(await screen.findByText('Secret profile')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Log out' }))
    await user.click(await screen.findByRole('link', { name: 'Back to profile' }))

    expect(await screen.findByText('Log in or register first.')).toBeInTheDocument()
    expect(screen.queryByText('Secret profile')).not.toBeInTheDocument()
  })
})
