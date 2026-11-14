import { screen } from '@testing-library/react'
import { App } from '../../App.tsx'
import type { OwnProfile } from '../../shared/api/types.ts'
import { mockFetch, problem, renderRoutes } from '../../test/utils.ts'

const rocky: OwnProfile = {
  username: 'Rocky',
  email: 'user-1@example.test',
  joinedAt: '2026-11-14T10:00:00Z',
  role: 'player',
}

function renderHome() {
  return renderRoutes({ '/*': <App /> }, '/')
}

describe('HomePage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers a Visitor to create an account or log in (AC-01, AC-07)', async () => {
    mockFetch(() =>
      problem(401, { title: 'Log in or register first.', errorCode: 'identity.not_authenticated' }),
    )
    renderHome()

    expect(await screen.findByRole('link', { name: 'Create account' })).toHaveAttribute(
      'href',
      '/register',
    )
    expect(screen.getByRole('link', { name: 'Log in' })).toHaveAttribute('href', '/login')
    expect(screen.queryByRole('link', { name: 'My profile' })).not.toBeInTheDocument()
  })

  it('offers a logged-in Player their profile instead', async () => {
    mockFetch(() => Response.json(rocky))
    renderHome()

    expect(await screen.findByRole('link', { name: 'My profile' })).toHaveAttribute(
      'href',
      '/profile',
    )
    expect(screen.queryByRole('link', { name: 'Create account' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Log in' })).not.toBeInTheDocument()
  })
})
