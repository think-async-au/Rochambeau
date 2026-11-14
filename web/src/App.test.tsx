import { screen } from '@testing-library/react'
import { App } from './App.tsx'
import { mockFetch, problem, renderRoutes } from './test/utils.ts'

describe('App shell', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders the home page', () => {
    mockFetch(() =>
      problem(401, { title: 'Log in or register first.', errorCode: 'identity.not_authenticated' }),
    )
    renderRoutes({ '/*': <App /> }, '/')

    expect(screen.getByRole('heading', { name: 'Rochambeau' })).toBeInTheDocument()
  })
})
