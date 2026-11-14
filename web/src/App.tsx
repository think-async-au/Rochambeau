import { Route, Routes } from 'react-router'
import { RegisterPage } from './features/auth/RegisterPage.tsx'

function Home() {
  return (
    <main className="mx-auto flex min-h-screen max-w-3xl flex-col items-center justify-center gap-2 p-6">
      <h1 className="text-4xl font-bold">Rochambeau</h1>
      <p className="text-gray-600">Rock · Paper · Scissors</p>
    </main>
  )
}

export function App() {
  return (
    <Routes>
      <Route path="/" element={<Home />} />
      <Route path="/register" element={<RegisterPage />} />
    </Routes>
  )
}
