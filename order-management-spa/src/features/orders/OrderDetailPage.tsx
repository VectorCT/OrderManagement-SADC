import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { formatMoney } from '../../lib/sadc';
import { getCustomer, getOrder, transitionOrder } from './detailApi';

const VALID_ACTIONS: Record<string, ('Paid' | 'Fulfilled' | 'Cancelled')[]> = {
  Pending: ['Paid', 'Cancelled'],
  Paid: ['Fulfilled', 'Cancelled'],
  Fulfilled: [],
  Cancelled: [],
};

export function OrderDetailPage(props: { id?: string } = {}) {
  const params = useParams();
  const id = props.id ?? params.id ?? '';
  const queryClient = useQueryClient();
  const [feedback, setFeedback] = useState('');
  const intentKeys = useRef(new Map<string, string>());

  const orderQuery = useQuery({
    queryKey: ['order', id],
    queryFn: () => getOrder(id),
    refetchInterval: (query) =>
      query.state.data && ['Pending', 'Paid'].includes(query.state.data.status) ? 4000 : false,
  });

  const customerQuery = useQuery({
    queryKey: ['customer', orderQuery.data?.customerId],
    queryFn: () => getCustomer(orderQuery.data!.customerId),
    enabled: !!orderQuery.data?.customerId,
  });

  const mutation = useMutation({
    mutationFn: ({ status }: { status: 'Paid' | 'Fulfilled' | 'Cancelled' }) => {
      const intent = `${id}:${status}`;
      let key = intentKeys.current.get(intent);
      if (!key) {
        key = crypto.randomUUID();
        intentKeys.current.set(intent, key);
      }
      return transitionOrder(id, status, key);
    },
    onSuccess: (order) => {
      intentKeys.current.clear();
      setFeedback(`Order status changed to ${order.status}.`);
      void queryClient.invalidateQueries({ queryKey: ['order', id] });
    },
  });

  function requestTransition(status: 'Paid' | 'Fulfilled' | 'Cancelled') {
    if (status === 'Cancelled' && !window.confirm('Cancel this order? This action cannot be reversed.')) return;
    void mutation.mutateAsync({ status }).catch((error: unknown) => {
      if (error instanceof ApiError && error.status === 409) {
        void queryClient.invalidateQueries({ queryKey: ['order', id] });
      }
    });
  }

  if (orderQuery.isLoading) return <p role="status" aria-busy="true">Loading order...</p>;
  if (orderQuery.isError) return <div role="alert"><p>{orderQuery.error.message}</p><button type="button" onClick={() => void orderQuery.refetch()}>Retry</button></div>;

  const order = orderQuery.data!;
  const actions = VALID_ACTIONS[order.status] ?? [];

  return (
    <section aria-labelledby="order-detail-heading">
      <h1 id="order-detail-heading">Order {order.id}</h1>
      <div role="status" aria-live="polite">{feedback}</div>
      <dl>
        <dt>Customer</dt><dd>{customerQuery.data?.name ?? order.customerId}</dd>
        <dt>Status</dt><dd>{order.status}</dd>
        <dt>Currency</dt><dd>{order.currencyCode}</dd>
        <dt>Created</dt><dd>{new Date(order.createdAt).toLocaleString()}</dd>
        <dt>Updated</dt><dd>{order.updatedAt ? new Date(order.updatedAt).toLocaleString() : 'Not updated'}</dd>
        <dt>Server total</dt><dd>{formatMoney(order.totalAmount, order.currencyCode)}</dd>
      </dl>

      <table>
        <caption>Order line items</caption>
        <thead><tr><th scope="col">Product SKU</th><th scope="col">Quantity</th><th scope="col">Unit price</th></tr></thead>
        <tbody>
          {order.lineItems.map((item) => (
            <tr key={item.id}><td>{item.productSku}</td><td>{item.quantity}</td><td>{formatMoney(item.unitPrice, order.currencyCode)}</td></tr>
          ))}
        </tbody>
      </table>

      {actions.length > 0 && (
        <section aria-labelledby="actions-heading">
          <h2 id="actions-heading">Status actions</h2>
          {actions.map((status) => (
            <button key={status} type="button" disabled={mutation.isPending} onClick={() => requestTransition(status)}>
              {status === 'Paid' ? 'Mark paid' : status === 'Fulfilled' ? 'Mark fulfilled' : 'Cancel order'}
            </button>
          ))}
        </section>
      )}
      {mutation.error && (
        <p role="alert">
          {mutation.error instanceof ApiError
            ? mutation.error.status === 422
              ? `422 validation: ${mutation.error.message}`
              : mutation.error.status === 409
                ? `409 conflict: ${mutation.error.message}`
                : mutation.error.message
            : 'Status update failed.'}
        </p>
      )}
    </section>
  );
}
