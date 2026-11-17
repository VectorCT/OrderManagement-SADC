import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function RouteGuard() {
  const { token } = useAuth();
  return token ? <Outlet /> : <Navigate to="/login" replace />;
}
