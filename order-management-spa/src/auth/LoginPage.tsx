import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Link } from 'react-router-dom';
import { useAuth, type DevelopmentRole } from './AuthContext';
import styles from './Auth.module.css';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<DevelopmentRole>('Orders.Read');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const registered = (location.state as { registered?: boolean } | null)?.registered;

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      await login(email, password, role);
      navigate('/customers', { replace: true });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className={styles.page} aria-labelledby="login-heading">
      <section className={styles.brandPanel}>
        <div className={styles.logo}><span className={styles.logoMark}>OD</span> Order Desk</div>
        <h1>Manage every SADC order.</h1>
        <p>Customers, currencies and fulfilment in one calm, dependable workspace.</p>
      </section>
      <section className={styles.formPanel}>
        <div className={styles.card}>
          <h2 id="login-heading">Welcome back</h2>
          <p className={styles.subtitle}>Sign in to continue to your workspace.</p>
          {registered && <p className={styles.success} role="status">Account created successfully. Sign in to continue.</p>}
          <form className={styles.form} onSubmit={handleSubmit}>
            <div className={styles.field}>
              <label htmlFor="email">Email address</label>
              <input id="email" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="password">Password</label>
              <input id="password" type="password" autoComplete="current-password" value={password} onChange={(event) => setPassword(event.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="role">Development role</label>
              <select id="role" value={role} onChange={(event) => setRole(event.target.value as DevelopmentRole)}>
                <option value="Orders.Read">Read</option>
                <option value="Orders.Write">Write</option>
                <option value="Orders.Admin">Admin</option>
              </select>
            </div>
            {error && <p className={styles.error} role="alert">{error}</p>}
            <button className={styles.submit} type="submit" disabled={submitting}>{submitting ? 'Signing in...' : 'Sign in'}</button>
          </form>
          <p className={styles.switch}>New to Order Desk? <Link to="/register">Create an account</Link></p>
        </div>
      </section>
    </main>
  );
}
