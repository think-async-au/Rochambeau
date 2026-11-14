import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { meQueryKey } from '../../shared/api/me.ts'
import { mockFetch, problem, renderRoutes, requestBody } from '../../test/utils.ts'
import { RegisterPage } from './RegisterPage.tsx'

const usernameRule =
  'The username must be 3 to 20 characters: Latin letters, digits, underscore or hyphen.'

function renderRegister() {
  return renderRoutes(
    { '/register': <RegisterPage />, '/profile': <p>Own profile page</p> },
    '/register',
  )
}

async function fillAndSubmit(
  username = 'Rocky',
  email = 'user-1@example.test',
  password = 'test-password-8',
) {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Username'), username)
  await user.type(screen.getByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.click(screen.getByRole('button', { name: 'Create account' }))
  return user
}

describe('RegisterPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('registers, caches the own profile and opens it (AC-01)', async () => {
    const profile = {
      username: 'Rocky',
      email: 'user-1@example.test',
      joinedAt: '2026-11-14T10:00:00Z',
      role: 'player',
    }
    const fetchMock = mockFetch(() => Response.json(profile, { status: 201 }))
    const { queryClient } = renderRegister()

    await fillAndSubmit()

    expect(await screen.findByText('Own profile page')).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/v1/accounts')
    expect(init?.method).toBe('POST')
    expect(requestBody(init)).toEqual({
      username: 'Rocky',
      email: 'user-1@example.test',
      password: 'test-password-8',
    })
    expect(queryClient.getQueryData(meQueryKey)).toEqual(profile)
  })

  it('shows every field error next to its field, keeps the input and clears the password (AC-02)', async () => {
    mockFetch(() =>
      problem(400, {
        title: 'One or more fields are invalid.',
        errorCode: 'identity.validation_failed',
        errors: {
          username: [usernameRule],
          password: ['The password must be at least 8 characters.'],
        },
      }),
    )
    renderRegister()

    await fillAndSubmit('ab', 'user-1@example.test', 'short')

    const username = screen.getByLabelText('Username')
    const password = screen.getByLabelText('Password')
    expect(await screen.findByText(usernameRule)).toBeInTheDocument()
    expect(username).toHaveAccessibleDescription(usernameRule)
    expect(password).toHaveAccessibleDescription('The password must be at least 8 characters.')
    expect(screen.getByLabelText('Email')).not.toHaveAccessibleDescription()
    expect(username).toHaveValue('ab')
    expect(screen.getByLabelText('Email')).toHaveValue('user-1@example.test')
    expect(password).toHaveValue('')
    expect(username).toHaveAttribute('aria-invalid', 'true')
  })

  it.each([
    ['identity.username_taken', 'This username is already taken.', 'Username'],
    ['identity.email_unavailable', 'This email address cannot be used.', 'Email'],
  ])('shows %s under the matching field (AC-03)', async (errorCode, title, field) => {
    mockFetch(() => problem(409, { title, errorCode }))
    renderRegister()

    await fillAndSubmit()

    expect(await screen.findByText(title)).toBeInTheDocument()
    expect(screen.getByLabelText(field)).toHaveAccessibleDescription(title)
    expect(screen.getByLabelText('Username')).toHaveValue('Rocky')
    expect(screen.getByLabelText('Email')).toHaveValue('user-1@example.test')
    expect(screen.getByLabelText('Password')).toHaveValue('')
  })

  it('shows the cap message for the whole form (AC-15)', async () => {
    const title = 'Too many accounts were created from your connection recently. Try again later.'
    mockFetch(() => problem(429, { title, errorCode: 'identity.registration_cap' }))
    renderRegister()

    await fillAndSubmit()

    expect(await screen.findByRole('alert')).toHaveTextContent(title)
  })

  it('sends one request even when submit is clicked twice', async () => {
    let release: (response: Response) => void = () => {}
    const fetchMock = mockFetch(() => new Promise<Response>((resolve) => (release = resolve)))
    renderRegister()

    const user = await fillAndSubmit()
    const button = screen.getByRole('button', { name: /creating|create account/i })
    expect(button).toBeDisabled()
    await user.click(button)
    release(problem(429, { title: 'Try later.', errorCode: 'identity.registration_cap' }))

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('shows server text as plain text, never as markup', async () => {
    mockFetch(() => problem(409, { title: '<b>taken</b>', errorCode: 'identity.username_taken' }))
    renderRegister()

    await fillAndSubmit()

    expect(await screen.findByText('<b>taken</b>')).toBeInTheDocument()
  })
})
