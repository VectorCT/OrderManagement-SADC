import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from './AuthContext';
import styles from './Auth.module.css';

export function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setSubmitting(true);
    try {
      await register(firstName, lastName, email, password);
      navigate('/login', { replace: true, state: { registered: true } });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Account registration failed.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className={styles.page} aria-labelledby="register-heading">
      <section className={styles.brandPanel}>
        <div className={styles.logo}><span className={styles.logoMark}>OD</span> Order Desk</div>
        <h1>Build a better order workflow.</h1>
        <p>Create your account and join a single, trusted view of customers and fulfilment.</p>
      </section>
      <section className={styles.formPanel}>
        <div className={styles.card}>
          <h2 id="register-heading">Create account</h2>
          <p className={styles.subtitle}>Register with your name and work email.</p>
          <form className={styles.form} onSubmit={handleSubmit}>
            <div className={styles.fieldRow}>
              <div className={styles.field}>
                <label htmlFor="first-name">First name</label>
                <input id="first-name" autoComplete="given-name" value={firstName} onChange={(event) => setFirstName(event.target.value)} required />
              </div>
              <div className={styles.field}>
                <label htmlFor="last-name">Last name</label>
                <input id="last-name" autoComplete="family-name" value={lastName} onChange={(event) => setLastName(event.target.value)} required />
              </div>
            </div>
            <div className={styles.field}>
              <label htmlFor="register-email">Email address</label>
              <input id="register-email" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </div>
            <div className={styles.field}>
              <label htmlFor="register-password">Password</label>
              <input id="register-password" type="password" autoComplete="new-password" minLength={12} value={password} onChange={(event) => setPassword(event.target.value)} required />
              <small>Use at least 12 characters with uppercase, lowercase, a number, and a symbol.</small>
            </div>
            {error && <p className={styles.error} role="alert">{error}</p>}
            <button className={styles.submit} type="submit" disabled={submitting}>{submitting ? 'Creating account...' : 'Create account'}</button>
          </form>
          <p className={styles.switch}>Already have an account? <Link to="/login">Sign in</Link></p>
        </div>
      </section>
    </main>
  );
}
