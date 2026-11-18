import { useEffect, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import styles from '../workspace.module.css';
import adminStyles from './Admin.module.css';
import { createAdminUser, getAdminUser, updateAdminUser } from './api';

export function AdminUserFormPage() {
  const { userId } = useParams();
  const editing = !!userId;
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const userQuery = useQuery({
    queryKey: ['admin-user', userId],
    queryFn: () => getAdminUser(userId!),
    enabled: editing,
  });

  useEffect(() => {
    if (!userQuery.data) return;
    setFirstName(userQuery.data.firstName);
    setLastName(userQuery.data.lastName);
    setEmail(userQuery.data.email);
  }, [userQuery.data]);

  const mutation = useMutation({
    mutationFn: () => editing
      ? updateAdminUser(userId!, { firstName, lastName, email })
      : createAdminUser({ firstName, lastName, email, password }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['admin-users'] });
      navigate('/admin/users', { replace: true });
    },
  });

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    try {
      await mutation.mutateAsync();
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'Unable to save user.');
    }
  }

  if (editing && userQuery.isLoading) return <p role="status" aria-busy="true">Loading user...</p>;
  if (editing && userQuery.isError) return <p role="alert">{userQuery.error.message}</p>;

  return (
    <section className={styles.pageSection} aria-labelledby="user-form-heading">
      <div className={styles.titleRow}>
        <div>
          <p className={styles.breadcrumb}><Link to="/admin/users">User management</Link> / {editing ? 'Edit user' : 'New user'}</p>
          <h1 id="user-form-heading">{editing ? 'Edit user' : 'Add user'}</h1>
        </div>
        <Link className={styles.secondaryButton} to="/admin/users">Back to users</Link>
      </div>
      <div className={styles.formCard}>
        <form className={adminStyles.form} onSubmit={submit}>
          <label>First name<input value={firstName} onChange={(event) => setFirstName(event.target.value)} required maxLength={100} /></label>
          <label>Last name<input value={lastName} onChange={(event) => setLastName(event.target.value)} required maxLength={100} /></label>
          <label>Email<input type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required maxLength={256} /></label>
          {!editing && <label>Password<input type="password" autoComplete="new-password" value={password} onChange={(event) => setPassword(event.target.value)} required minLength={12} /><small>At least 12 characters with uppercase, lowercase, a number, and a symbol.</small></label>}
          {error && <p className={adminStyles.error} role="alert">{error}</p>}
          <button className={styles.primaryButton} type="submit" disabled={mutation.isPending}>{mutation.isPending ? 'Saving...' : editing ? 'Save changes' : 'Create user'}</button>
        </form>
      </div>
    </section>
  );
}
