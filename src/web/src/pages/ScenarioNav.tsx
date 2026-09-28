import { NavLink } from 'react-router-dom'

/** Secondary navigation shared by the scenario pages. */
export default function ScenarioNav() {
  return (
    <nav className="subnav">
      <NavLink to="/scenarios" end>
        Koşular
      </NavLink>
      <NavLink to="/scenarios/trends">Trendler</NavLink>
      <NavLink to="/scenarios/compare">Karşılaştır</NavLink>
    </nav>
  )
}
