import { Link } from 'react-router-dom';
import { CreateOrderForm } from './CreateOrderForm';
import styles from '../workspace.module.css';

export function CreateOrderPage() {
  return (
    <section className={styles.pageSection} aria-labelledby="new-order-heading">
      <div className={styles.titleRow}>
        <div>
          <p className={styles.breadcrumb}><Link to="/orders">Orders</Link> / New order</p>
          <h1 id="new-order-heading">New order</h1>
        </div>
        <Link className={styles.secondaryButton} to="/orders">Back to orders</Link>
      </div>
      <CreateOrderForm />
    </section>
  );
}
