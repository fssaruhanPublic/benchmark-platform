import { Link, NavLink, Navigate, Route, Routes } from 'react-router-dom'
import ComparePage from './pages/ComparePage'
import RunDetailPage from './pages/RunDetailPage'
import RunsPage from './pages/RunsPage'
import TrendsPage from './pages/TrendsPage'

export default function App() {
  return (
    <>
      <header className="app-header">
        <Link to="/runs" className="brand">
          Benchmark Dashboard
        </Link>
        <nav>
          <NavLink to="/runs">Koşular</NavLink>
          <NavLink to="/trends">Trendler</NavLink>
          <NavLink to="/compare">Karşılaştır</NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/runs" replace />} />
          <Route path="/runs" element={<RunsPage />} />
          <Route path="/runs/:id" element={<RunDetailPage />} />
          <Route path="/trends" element={<TrendsPage />} />
          <Route path="/compare" element={<ComparePage />} />
          <Route path="*" element={<div className="state">Sayfa bulunamadı.</div>} />
        </Routes>
      </main>
    </>
  )
}
