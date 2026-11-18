import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ApiError } from '../../api/client';
import styles from '../workspace.module.css';
import adminStyles from './Admin.module.css';
import { assignRoles, deleteAdminUser, getAvailableRoles, listUsers } from './api';

export function AdminUsersPage() {
  const queryClient = useQueryClient();
  const [selectedRoles, setSelectedRoles] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState('');
  const usersQuery = useQuery({ queryKey: ['admin-users'], queryFn: () => listUsers() });
  const rolesQuery = useQuery({ queryKey: ['available-roles'], queryFn: getAvailableRoles });
  const assignMutation = useMutation({
    mutationFn: ({ id, role }: { id: string; role: string }) => assignRoles(id, [role]),
    onSuccess: () => {
      setFeedback('Role assigned successfully.');
      void queryClient.invalidateQueries({ queryKey: ['admin-users'] });
    },
  });
  const deleteMutation = useMutation({
    mutationFn: deleteAdminUser,
    onSuccess: () => {
      setFeedback('User deleted.');
      void queryClient.invalidateQueries({ queryKey: ['admin-users'] });
    },
  });

  async function removeUser(id: string) {
    if (!window.confirm('Delete this user? This cannot be undone.')) return;
    try {
      await deleteMutation.mutateAsync(id);
    } catch {
      setFeedback(deleteMutation.error instanceof Error ? deleteMutation.error.message : 'Unable to delete user.');
    }
  }

  async function grantRole(id: string) {
    const role = selectedRoles[id];
    if (!role) return;
    try {
      await assignMutation.mutateAsync({ id, role });
      setSelectedRoles((selected) => ({ ...selected, [id]: '' }));
    } catch {
      setFeedback(assignMutation.error instanceof Error ? assignMutation.error.message : 'Unable to assign role.');
    }
  }

  return (
    <section className={styles.pageSection} aria-labelledby="admin-users-heading">
      <div className={styles.titleRow}>
        <div>
          <p className={styles.breadcrumb}>Administration / Users</p>
          <h1 id="admin-users-heading">User management</h1>
        </div>
        <Link className={styles.primaryButton} to="/admin/users/new">Add user</Link>
      </div>
      <p className={adminStyles.muted}>Manage registered accounts and grant access roles. Users with no roles cannot access protected workflows.</p>
      <div role="status" aria-live="polite">{feedback}</div>
      {(assignMutation.isError || deleteMutation.isError) && (
        <p className={adminStyles.error} role="alert">
          {(assignMutation.error ?? deleteMutation.error) instanceof ApiError
            ? (assignMutation.error ?? deleteMutation.error)?.message
            : 'The requested user action failed.'}
        </p>
      )}
      {usersQuery.isLoading && <p role="status" aria-busy="true">Loading users...</p>}
      {usersQuery.isError && <div role="alert"><p>{usersQuery.error.message}</p><button type="button" onClick={() => void usersQuery.refetch()}>Retry</button></div>}
      {rolesQuery.isError && <div role="alert"><p>{rolesQuery.error.message}</p><button type="button" onClick={() => void rolesQuery.refetch()}>Retry loading roles</button></div>}
      {usersQuery.isSuccess && (
        <div className={`${styles.tableCard} ${adminStyles.tableWrap}`}>
          <table className={adminStyles.table}>
            <caption>Registered users and their access roles</caption>
            <thead><tr><th scope="col">User</th><th scope="col">Registered</th><th scope="col">Roles</th><th scope="col">Assign role</th><th scope="col">Actions</th></tr></thead>
            <tbody>
              {usersQuery.data.items.map((user) => (
                <tr key={user.id}>
                  <td><strong>{user.firstName} {user.lastName}</strong><br /><span className={adminStyles.muted}>{user.email}</span></td>
                  <td>{new Date(user.createdAt).toLocaleDateString()}</td>
                  <td>{user.roles.length ? user.roles.join(', ') : <span className={adminStyles.error}>No roles</span>}</td>
                  <td>
                    <div className={adminStyles.actions}>
                      <select aria-label={`Role for ${user.email}`} value={selectedRoles[user.id] ?? ''} onChange={(event) => setSelectedRoles((selected) => ({ ...selected, [user.id]: event.target.value }))}>
                        <option value="">Select role</option>
                        {rolesQuery.data?.map((role) => <option key={role} value={role}>{role}</option>)}
                      </select>
                      <button type="button" disabled={!selectedRoles[user.id] || assignMutation.isPending} onClick={() => void grantRole(user.id)}>Grant</button>
                    </div>
                  </td>
                  <td>
                    <div className={adminStyles.actions}>
                      <Link className={styles.secondaryButton} to={`/admin/users/${encodeURIComponent(user.id)}/edit`}>Edit</Link>
                      <button className={styles.dangerButton} type="button" disabled={deleteMutation.isPending} onClick={() => void removeUser(user.id)}>Delete</button>
                    </div>
                  </td>
                </tr>
              ))}
              {usersQuery.data.items.length === 0 && <tr><td colSpan={5}>No registered users.</td></tr>}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
