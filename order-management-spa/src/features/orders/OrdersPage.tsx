import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { formatMoney } from '../../lib/sadc';
import { listOrders, searchCustomers } from './api';
import { CreateOrderForm } from './CreateOrderForm';

export function OrdersPage() {
  const [params, setParams] = useSearchParams();
  const customerId = params.get('customerId') ?? '';
  const status = params.get('status') ?? '';
  const sort = params.get('sort') ?? '';
  const page = Math.max(1, Number(params.get('page') ?? 1) || 1);
  const pageSize = Math.min(100, Math.max(1, Number(params.get('pageSize') ?? 20) || 20));
  const customerQuery = useQuery({
    queryKey: ['order-list-customers'],
    queryFn: () => searchCustomers(''),
  });

  const queryParams = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (customerId) queryParams.set('customerId', customerId);
  if (status) queryParams.set('status', status);
  if (sort) queryParams.set('sort', sort);

  const query = useQuery({
    queryKey: ['orders', { customerId, status, sort, page, pageSize }],
    queryFn: () => listOrders(queryParams),
  });

  function update(name: string, value: string) {
    setParams((current) => {
      if (value) current.set(name, value);
      else current.delete(name);
      if (name !== 'page') current.set('page', '1');
      return current;
    });
  }

  return (
    <section aria-labelledby="orders-heading">
      <h1 id="orders-heading">Orders</h1>
      <label htmlFor="order-customer-filter">Filter by customer</label>
      <select id="order-customer-filter" value={customerId} onChange={(event) => update('customerId', event.target.value)}>
        <option value="">All customers</option>
        {customerQuery.data?.items.map((customer) => <option key={customer.id} value={customer.id}>{customer.name}</option>)}
      </select>
      <label htmlFor="order-status">Status</label>
      <select id="order-status" value={status} onChange={(event) => update('status', event.target.value)}>
        <option value="">All statuses</option>
        {['Pending', 'Paid', 'Fulfilled', 'Cancelled'].map((value) => <option key={value} value={value}>{value}</option>)}
      </select>
      <label htmlFor="order-sort">Sort</label>
      <select id="order-sort" value={sort} onChange={(event) => update('sort', event.target.value)}>
        <option value="">Default</option>
        <option value="date">Date</option>
        <option value="total">Total</option>
      </select>
      <label htmlFor="orders-page-size">Page size</label>
      <select id="orders-page-size" value={pageSize} onChange={(event) => update('pageSize', event.target.value)}>
        {[10, 20, 50, 100].map((size) => <option key={size} value={String(size)}>{size}</option>)}
      </select>

      {query.isLoading && <p role="status" aria-busy="true">Loading orders...</p>}
      {query.isError && <div role="alert"><p>{query.error instanceof Error ? query.error.message : 'Unable to load orders.'}</p><button type="button" onClick={() => void query.refetch()}>Retry</button></div>}
      {query.isSuccess && query.data.items.length === 0 && <p>No orders found.</p>}
      {query.isSuccess && query.data.items.length > 0 && (
        <>
        <table>
          <caption>Order search results</caption>
          <thead><tr><th scope="col">Order</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Server total</th></tr></thead>
          <tbody>
            {query.data.items.map((order) => (
              <tr key={order.id}>
                <td><a href={`/orders/${order.id}`}>{order.id}</a></td>
                <td>{order.status}</td>
                <td>{new Date(order.createdAt).toLocaleString()}</td>
                <td>{formatMoney(order.totalAmount, order.currencyCode)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <nav aria-label="Order pagination">
          <button type="button" disabled={page <= 1} onClick={() => update('page', String(page - 1))}>Previous</button>
          <span>Page {page}</span>
          <button type="button" disabled={query.data.items.length < pageSize} onClick={() => update('page', String(page + 1))}>Next</button>
        </nav>
        </>
      )}
      <CreateOrderForm />
    </section>
  );
}
