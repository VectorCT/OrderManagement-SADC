import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from './AuthContext';

export function AdminRouteGuard() {
  const { isAdmin } = useAuth();
  return isAdmin ? <Outlet /> : <Navigate to="/customers" replace />;
}
