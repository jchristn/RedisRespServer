import { Routes, Route, Navigate } from 'react-router-dom';
import { useApp } from './context/AppContext';
import ErrorBoundary from './components/ErrorBoundary';
import Login from './views/Login';
import Dashboard from './views/Dashboard';
import Overview from './views/Overview';
import Databases from './views/Databases';
import Keys from './views/Keys';
import Clients from './views/Clients';
import Console from './views/Console';

export default function App() {
  const { isConnected } = useApp();

  return (
    <ErrorBoundary>
      <Routes>
        <Route
          path="/"
          element={isConnected ? <Navigate to="/overview" replace /> : <Login />}
        />
        <Route path="/" element={<Dashboard />}>
          <Route path="overview" element={<ErrorBoundary><Overview /></ErrorBoundary>} />
          <Route path="databases" element={<ErrorBoundary><Databases /></ErrorBoundary>} />
          <Route path="keys" element={<ErrorBoundary><Keys /></ErrorBoundary>} />
          <Route path="clients" element={<ErrorBoundary><Clients /></ErrorBoundary>} />
          <Route path="console" element={<ErrorBoundary><Console /></ErrorBoundary>} />
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </ErrorBoundary>
  );
}
