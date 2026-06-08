import { BrowserRouter, Routes, Route, NavLink, Navigate } from 'react-router-dom'
import { useEffect, useState } from 'react'
import './App.css'
import Mnemoscheme from './pages/Mnemoscheme'
import SiloDetail from './pages/SiloDetail'
import Trends from './pages/Trends'
import Alarms from './pages/Alarms'
import Reports from './pages/Reports'
import ThresholdConfig from './pages/ThresholdConfig'
import Configurator from './pages/Configurator'
import { onAlertCounts } from './services/signalr'

function MainLayout() {
  const [unacknowledged, setUnacknowledged] = useState(0)

  useEffect(() => {
    return onAlertCounts((counts) => {
      setUnacknowledged(counts.unacknowledged)
    })
  }, [])

  return (
    <div className="app">
      <nav className="navbar">
        <div className="nav-brand">Термометрия элеватора</div>
        <div className="nav-links">
          <NavLink to="/" end>Мнемосхема</NavLink>
          <NavLink to="/trends">Тренды</NavLink>
          <NavLink to="/alarms">
            Алармы
            {unacknowledged > 0 && <span className="nav-alert-badge">{unacknowledged}</span>}
          </NavLink>
          <NavLink to="/reports">Отчёты</NavLink>
          <NavLink to="/thresholds">Пороги</NavLink>
        </div>
      </nav>
      <main className="main-content">
        <Routes>
          <Route path="/" element={<Mnemoscheme />} />
          <Route path="/silo/:id" element={<SiloDetail />} />
          <Route path="/trends" element={<Trends />} />
          <Route path="/alarms" element={<Alarms />} />
          <Route path="/reports" element={<Reports />} />
          <Route path="/thresholds" element={<ThresholdConfig />} />
          <Route path="/config" element={<Configurator />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/*" element={<MainLayout />} />
      </Routes>
    </BrowserRouter>
  )
}
