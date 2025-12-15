import { Outlet, Navigate } from 'react-router-dom';
import { useApp } from '../context/AppContext';
import Topbar from '../components/Topbar';
import Sidebar from '../components/Sidebar';
import './Dashboard.css';

export default function Dashboard() {
  const { isConnected, isConnecting } = useApp();

  if (isConnecting) {
    return (
      <div className="dashboard-loading">
        <div className="loading-spinner"></div>
        <span>Connecting...</span>
      </div>
    );
  }

  if (!isConnected) {
    return <Navigate to="/" replace />;
  }

  return (
    <div className="dashboard">
      <Topbar />
      <Sidebar />
      <main className="dashboard-content">
        <Outlet />
      </main>
    </div>
  );
}
