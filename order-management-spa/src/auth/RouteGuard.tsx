import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function RouteGuard() {
  const { token, isInitializing, authError, restoreSession } = useAuth();
  if (isInitializing) return <p role="status">Restoring your session…</p>;
  if (authError) {
    return (
      <section>
        <p role="alert">Could not restore your session: {authError}</p>
        <button type="button" onClick={() => void restoreSession()}>Retry</button>
      </section>
    );
  }
  return token ? <Outlet /> : <Navigate to="/login" replace />;
}
