import { Route, Routes } from 'react-router'
import { LoginPage } from './features/auth/LoginPage.tsx'
import { RegisterPage } from './features/auth/RegisterPage.tsx'
import { RequireAuth } from './features/auth/RequireAuth.tsx'
import { HomePage } from './features/home/HomePage.tsx'
import { OwnProfilePage } from './features/profile/OwnProfilePage.tsx'
import { PublicProfilePage } from './features/profile/PublicProfilePage.tsx'

export function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route path="/profile" element={<OwnProfilePage />} />
        <Route path="/players/:username" element={<PublicProfilePage />} />
      </Route>
    </Routes>
  )
}
