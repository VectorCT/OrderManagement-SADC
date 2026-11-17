import { Link, Outlet } from 'react-router-dom';
import styles from './App.module.css';

function App() {
  return (
    <div className={styles.app}>
      <header className={styles.header}>
        <Link className={styles.brand} to="/">Order Management · SADC</Link>
        <nav aria-label="Primary navigation">
          <Link to="/customers">Customers</Link>
          <Link to="/orders">Orders</Link>
        </nav>
      </header>
      <main className={styles.page}>
        <Outlet />
      </main>
    </div>
  );
}

export default App;
