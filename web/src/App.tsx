import { Route, Routes } from 'react-router'
import { LoginPage } from './features/auth/LoginPage.tsx'
import { RegisterPage } from './features/auth/RegisterPage.tsx'
import { RequireAuth } from './features/auth/RequireAuth.tsx'
import { OwnProfilePage } from './features/profile/OwnProfilePage.tsx'
import { PublicProfilePage } from './features/profile/PublicProfilePage.tsx'

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
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/profile" element={<OwnProfilePage />} />
        <Route path="/players/:username" element={<PublicProfilePage />} />
      </Route>
    </Routes>
  )
}
