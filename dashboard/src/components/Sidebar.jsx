import { NavLink } from 'react-router-dom';
import './Sidebar.css';

const navItems = [
  { path: '/overview', icon: '📊', label: 'Overview' },
  { path: '/databases', icon: '🗄️', label: 'Databases' },
  { path: '/keys', icon: '🔑', label: 'Keys' },
  { path: '/clients', icon: '👥', label: 'Clients' },
  { path: '/console', icon: '💻', label: 'Console' },
];

export default function Sidebar() {
  return (
    <aside className="sidebar">
      <nav className="sidebar-nav">
        {navItems.map(item => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `sidebar-link ${isActive ? 'active' : ''}`
            }
          >
            <span className="sidebar-icon">{item.icon}</span>
            <span className="sidebar-label">{item.label}</span>
          </NavLink>
        ))}
      </nav>

      <div className="sidebar-footer">
        <div className="sidebar-help">
          <a href="https://redis.io/docs/" target="_blank" rel="noopener noreferrer">
            Redis Docs
          </a>
        </div>
      </div>
    </aside>
  );
}
