import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { meQueryKey } from '../../shared/api/me.ts'
import { mockFetch, problem, renderRoutes, requestBody } from '../../test/utils.ts'
import { formatRetryTime } from './lockout.ts'
import { LoginPage } from './LoginPage.tsx'

function renderLogin() {
  return renderRoutes({ '/login': <LoginPage />, '/profile': <p>Own profile page</p> }, '/login')
}

// type="email" inputs drop surrounding spaces in the browser; the server trims too (AC-04, integration-tested).
async function fillAndSubmit(email = 'User-1@Example.test', password = 'test-password-8') {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.click(screen.getByRole('button', { name: 'Log in' }))
}

describe('LoginPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('logs in, caches the own profile and opens it (AC-04, AC-13)', async () => {
    const profile = {
      username: 'Rocky',
      email: 'user-1@example.test',
      joinedAt: '2026-11-14T10:00:00Z',
      role: 'administrator',
    }
    const fetchMock = mockFetch(() => Response.json(profile))
    const { queryClient } = renderLogin()

    await fillAndSubmit()

    expect(await screen.findByText('Own profile page')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/v1/session')
    expect(init?.method).toBe('POST')
    expect(requestBody(init)).toEqual({ email: 'User-1@Example.test', password: 'test-password-8' })
    expect(queryClient.getQueryData(meQueryKey)).toEqual(profile)
  })

  it('shows the shared message and stays on the form (AC-05)', async () => {
    mockFetch(() =>
      problem(401, {
        title: 'Email or password is incorrect.',
        errorCode: 'identity.invalid_credentials',
      }),
    )
    renderLogin()

    await fillAndSubmit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')
    expect(screen.getByRole('button', { name: 'Log in' })).toBeEnabled()
    expect(screen.getByLabelText('Email')).toHaveValue('User-1@Example.test')
    expect(screen.getByLabelText('Password')).toHaveValue('')
    expect(screen.queryByText('Own profile page')).not.toBeInTheDocument()
  })

  it('says login is blocked and when to try again (AC-06)', async () => {
    const lockoutEnd = '2026-11-14T10:15:00Z'
    mockFetch(() =>
      problem(423, {
        title: 'Login is temporarily blocked. Try again after the time shown.',
        errorCode: 'identity.locked_out',
        lockoutEnd,
      }),
    )
    renderLogin()

    await fillAndSubmit()

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Login is temporarily blocked. Try again after the time shown.')
    expect(alert).toHaveTextContent(formatRetryTime(new Date(lockoutEnd)))
  })

  it('shows missing fields next to them', async () => {
    mockFetch(() =>
      problem(400, {
        title: 'One or more fields are invalid.',
        errorCode: 'identity.validation_failed',
        errors: { password: ['The password is required.'] },
      }),
    )
    renderLogin()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('Email'), 'user@example.test')
    await user.click(screen.getByRole('button', { name: 'Log in' }))

    expect(await screen.findByText('The password is required.')).toBeInTheDocument()
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription(
      'The password is required.',
    )
  })
})

describe('formatRetryTime', () => {
  it('shows the local date and time with minutes', () => {
    const text = formatRetryTime(new Date('2026-11-14T10:15:00Z'))

    expect(text).toMatch(/\d{1,2}.\d{2}/)
  })
})
