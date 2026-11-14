import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App } from '../../App.tsx'
import type { OwnProfile } from '../../shared/api/types.ts'
import { mockFetch, problem, renderRoutes } from '../../test/utils.ts'
import { formatJoinDate } from './format.ts'

const rocky: OwnProfile = {
  username: 'Rocky',
  email: 'user-1@example.test',
  joinedAt: '2026-11-14T10:00:00Z',
  role: 'player',
}

function renderApp(route: string) {
  return renderRoutes({ '/*': <App /> }, route)
}

describe('OwnProfilePage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows username, email, join date and the Player role (AC-09)', async () => {
    mockFetch(() => Response.json(rocky))
    renderApp('/profile')

    expect(await screen.findByRole('heading', { name: 'Rocky' })).toBeInTheDocument()
    expect(screen.getByText('user-1@example.test')).toBeInTheDocument()
    expect(screen.getByText(formatJoinDate(rocky.joinedAt))).toBeInTheDocument()
    expect(screen.getByText('Player')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Log out' })).toBeInTheDocument()
  })

  it('labels an Administrator (AC-13)', async () => {
    mockFetch(() => Response.json({ ...rocky, role: 'administrator' }))
    renderApp('/profile')

    expect(await screen.findByText('Administrator')).toBeInTheDocument()
    expect(screen.queryByText('Player')).not.toBeInTheDocument()
  })

  it('looks a player up by the exact username, URL-encoded', async () => {
    const fetchMock = mockFetch((url) =>
      url === '/api/v1/me'
        ? Response.json(rocky)
        : Response.json({ username: 'Sci ss/x', joinedAt: rocky.joinedAt, role: 'player' }),
    )
    renderApp('/profile')
    const user = userEvent.setup()
    await screen.findByRole('heading', { name: 'Rocky' })

    await user.type(screen.getByLabelText('Look up a player'), ' Sci ss/x ')
    await user.click(screen.getByRole('button', { name: 'Open profile' }))

    expect(await screen.findByRole('heading', { name: 'Sci ss/x' })).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledWith('/api/v1/profiles/Sci%20ss%2Fx', expect.anything())
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })
})

describe('PublicProfilePage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows username, join date and role, and never an email (AC-11)', async () => {
    mockFetch((url) =>
      url === '/api/v1/me'
        ? Response.json(rocky)
        : Response.json({
            username: 'Scissors',
            joinedAt: '2026-11-14T09:30:00Z',
            role: 'player',
            email: 'leak@example.test',
          }),
    )
    renderApp('/players/scissors')

    expect(await screen.findByRole('heading', { name: 'Scissors' })).toBeInTheDocument()
    expect(screen.getByText(formatJoinDate('2026-11-14T09:30:00Z'))).toBeInTheDocument()
    expect(screen.getByText('Player')).toBeInTheDocument()
    expect(screen.queryByText(/leak@example\.test/)).not.toBeInTheDocument()
    expect(screen.queryByText(/email/i)).not.toBeInTheDocument()
  })

  it('says no player with that username exists (AC-12)', async () => {
    mockFetch((url) =>
      url === '/api/v1/me'
        ? Response.json(rocky)
        : problem(404, {
            title: 'No player with that username exists.',
            errorCode: 'identity.player_not_found',
          }),
    )
    renderApp('/players/sciss')

    expect(await screen.findByText('No player with that username exists.')).toBeInTheDocument()
  })

  it('asks to log in again when the session ended while the page was open (AC-08, AC-13b)', async () => {
    mockFetch((url) =>
      url === '/api/v1/me'
        ? Response.json(rocky)
        : problem(401, {
            title: 'Log in or register first.',
            errorCode: 'identity.not_authenticated',
          }),
    )
    renderApp('/players/scissors')

    expect(
      await screen.findByRole('heading', { name: 'Log in or register first.' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(/Could not load this profile/)).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login')
  })

  it('asks a Visitor to log in or register first and fetches no profile (AC-10)', async () => {
    const fetchMock = mockFetch(() =>
      problem(401, { title: 'Log in or register first.', errorCode: 'identity.not_authenticated' }),
    )
    renderApp('/players/scissors')

    expect(await screen.findByText('Log in or register first.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalledWith('/api/v1/profiles/scissors', expect.anything())
  })
})
