import { useQuery } from '@tanstack/react-query';
import { getMessagingDiagnostics } from './api';
import styles from '../workspace.module.css';
import adminStyles from './Admin.module.css';

export function MessagingDiagnosticsPage() {
  const query = useQuery({
    queryKey: ['messaging-diagnostics'],
    queryFn: getMessagingDiagnostics,
    refetchInterval: 15_000,
  });

  if (query.isLoading) return <p role="status" aria-busy="true">Loading message diagnostics...</p>;
  if (query.isError) return <div role="alert"><p>{query.error.message}</p><button type="button" onClick={() => void query.refetch()}>Retry</button></div>;
  const diagnostics = query.data;

  return (
    <section className={styles.pageSection} aria-labelledby="messaging-heading">
      <div className={styles.titleRow}>
        <div>
          <p className={styles.breadcrumb}>Administration / Operations</p>
          <h1 id="messaging-heading">Message diagnostics</h1>
        </div>
        <button className={styles.secondaryButton} type="button" onClick={() => void query.refetch()}>Refresh</button>
      </div>
      <p className={adminStyles.muted}>Updated {new Date(diagnostics.generatedAt).toLocaleString()}. Auto-refreshes every 15 seconds.</p>
      <div className={adminStyles.grid}>
        <div className={adminStyles.metric}><small>Orders created (24h)</small><strong>{diagnostics.ordersCreatedLast24Hours}</strong></div>
        <div className={adminStyles.metric}><small>Pending outbox</small><strong>{diagnostics.outbox.pendingCount}</strong></div>
        <div className={adminStyles.metric}><small>Retry scheduled</small><strong>{diagnostics.outbox.retryScheduledCount}</strong></div>
        <div className={adminStyles.metric}><small>Processed messages</small><strong>{diagnostics.processedMessageCount}</strong></div>
      </div>
      <div className={adminStyles.panel}>
        <h2>Broker status</h2>
        <p className={diagnostics.rabbitMq.isReachable ? adminStyles.statusOk : adminStyles.statusDown}>
          {diagnostics.rabbitMq.isReachable ? 'RabbitMQ reachable' : `RabbitMQ unavailable${diagnostics.rabbitMq.error ? `: ${diagnostics.rabbitMq.error}` : ''}`}
        </p>
        <div className={adminStyles.tableWrap}>
          <table className={adminStyles.table}>
            <caption>Order event queues</caption>
            <thead><tr><th scope="col">Queue</th><th scope="col">Exists</th><th scope="col">Ready</th><th scope="col">Consumers</th></tr></thead>
            <tbody>
              {diagnostics.rabbitMq.queues.map((queue) => (
                <tr key={queue.name}><td>{queue.name}</td><td>{queue.exists ? 'Yes' : 'No'}</td><td>{queue.readyMessages ?? 'Unavailable'}</td><td>{queue.consumers ?? 'Unavailable'}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
      <div className={adminStyles.panel}>
        <h2>Orders by status</h2>
        <div className={adminStyles.tableWrap}>
          <table className={adminStyles.table}>
            <caption>Order counts by status</caption>
            <thead><tr><th scope="col">Status</th><th scope="col">Count</th></tr></thead>
            <tbody>{diagnostics.ordersByStatus.map((item) => <tr key={item.status}><td>{item.status}</td><td>{item.count}</td></tr>)}</tbody>
          </table>
        </div>
      </div>
    </section>
  );
}
