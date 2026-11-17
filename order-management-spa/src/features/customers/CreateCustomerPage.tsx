import { Link, useNavigate } from 'react-router-dom';
import { CreateCustomerForm } from './CreateCustomerForm';
import styles from '../workspace.module.css';

export function CreateCustomerPage() {
  const navigate = useNavigate();

  return (
    <section className={styles.pageSection} aria-labelledby="new-customer-heading">
      <div className={styles.titleRow}>
        <div>
          <p className={styles.breadcrumb}><Link to="/customers">Customers</Link> / New customer</p>
          <h1 id="new-customer-heading">Add new customer</h1>
        </div>
        <Link className={styles.secondaryButton} to="/customers">Back to customers</Link>
      </div>
      <div className={styles.formCard}>
        <h2>Customer details</h2>
        <CreateCustomerForm onCreated={() => navigate('/customers', { replace: true, state: { customerCreated: true } })} />
      </div>
    </section>
  );
}
