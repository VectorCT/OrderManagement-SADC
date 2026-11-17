import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from './auth/AuthContext';
import styles from './App.module.css';

function App() {
  const { token, roles, isAdmin, logout } = useAuth();
  return (
    <div className={styles.app}>
      {token ? (
        <>
          <aside className={styles.sidebar}>
            <Link className={styles.brand} to="/orders">Order Desk</Link>
            <nav className={styles.nav} aria-label="Primary navigation">
              <NavLink to="/customers" className={({ isActive }) => isActive ? styles.active : ''}>Customers</NavLink>
              <NavLink to="/orders" className={({ isActive }) => isActive ? styles.active : ''}>Orders</NavLink>
              {isAdmin && <NavLink to="/admin/users" className={({ isActive }) => isActive ? styles.active : ''}>User management</NavLink>}
              {isAdmin && <NavLink to="/admin/messaging" className={({ isActive }) => isActive ? styles.active : ''}>Message diagnostics</NavLink>}
            </nav>
            <div className={styles.account}>
              <span className={styles.avatar}>{roles.length ? roles[0].charAt(0) : 'U'}</span>
              <div>
                <small>Signed in as</small>
                <strong>{roles.length ? roles.join(', ') : 'No roles assigned'}</strong>
              </div>
              <button type="button" onClick={logout} aria-label="Sign out">Log out</button>
            </div>
          </aside>
          <main className={styles.page}><Outlet /></main>
        </>
      ) : <main className={styles.authPage}><Outlet /></main>}
    </div>
  );
}

export default App;
