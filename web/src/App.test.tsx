import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { App } from './App.tsx'

describe('App shell', () => {
  it('renders the home page', () => {
    render(
      <MemoryRouter>
        <App />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'Rochambeau' })).toBeInTheDocument()
  })
})
