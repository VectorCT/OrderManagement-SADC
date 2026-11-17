import styles from './App.module.css';

function App() {
  return (
    <main className={styles.page}>
      <p className={styles.eyebrow}>Order Management · SADC</p>
      <h1>Frontend foundation is ready</h1>
      <p className={styles.description}>
        Customer and order workflows will be built here against the
        OrderManagement API.
      </p>
    </main>
  );
}

export default App;
