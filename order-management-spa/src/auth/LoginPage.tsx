import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth, type DevelopmentRole } from './AuthContext';

export function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState<DevelopmentRole>('Orders.Read');
  const [error, setError] = useState('');

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError('');
    try {
      await login(email, password, role);
      navigate('/customers', { replace: true });
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Sign-in failed.');
    }
  }

  return (
    <main aria-labelledby="login-heading">
      <h1 id="login-heading">Development sign in</h1>
      <form onSubmit={handleSubmit}>
        <label htmlFor="email">Email</label>
        <input id="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        <label htmlFor="password">Password</label>
        <input id="password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required />
        <label htmlFor="role">Development role</label>
        <select id="role" value={role} onChange={(event) => setRole(event.target.value as DevelopmentRole)}>
          <option value="Orders.Read">Read</option>
          <option value="Orders.Write">Write</option>
          <option value="Orders.Admin">Admin</option>
        </select>
        {error && <p role="alert">{error}</p>}
        <button type="submit">Sign in</button>
      </form>
    </main>
  );
}
