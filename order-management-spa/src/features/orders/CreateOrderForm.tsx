import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useForm, useFieldArray } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../../api/client';
import { CURRENCY_BY_COUNTRY } from '../../lib/sadc';
import { createOrder, searchCustomers } from './api';
import { orderSchema, type OrderFormData } from './schema';
import styles from '../workspace.module.css';

export function CreateOrderForm() {
  const navigate = useNavigate();
  const form = useForm<OrderFormData>({
    resolver: zodResolver(orderSchema),
    defaultValues: { customerId: '', currencyCode: '', lineItems: [{ productSku: '', quantity: 1, unitPrice: 0 }] },
  });
  const { fields, append, remove } = useFieldArray({ control: form.control, name: 'lineItems' });
  const customerQuery = useQuery({
    queryKey: ['order-customers'],
    queryFn: () => searchCustomers(''),
  });
  const selectedCustomerId = form.watch('customerId');
  const selectedCustomer = customerQuery.data?.items.find((customer) => customer.id === selectedCustomerId);
  const currencies = selectedCustomer ? CURRENCY_BY_COUNTRY[selectedCustomer.countryCode] ?? [] : [];
  const estimatedTotal = form.watch('lineItems').reduce(
    (sum, item) => sum + (Number(item.quantity) || 0) * (Number(item.unitPrice) || 0),
    0,
  );

  const mutation = useMutation({
    mutationFn: createOrder,
    onSuccess: (order) => navigate(`/orders/${order.id}`),
  });

  async function submit(values: OrderFormData) {
    try {
      await mutation.mutateAsync(values);
    } catch (error) {
      if (error instanceof ApiError && error.details.errors) {
        for (const [field, messages] of Object.entries(error.details.errors)) {
          const key = field.toLowerCase();
          if (key.includes('customer')) form.setError('customerId', { message: messages[0] });
          else if (key.includes('currency')) form.setError('currencyCode', { message: messages[0] });
        }
      }
    }
  }

  return (
    <section className={styles.formCard} aria-labelledby="create-order-heading">
      <h2 id="create-order-heading">Order details</h2>
      <form onSubmit={form.handleSubmit(submit)} noValidate aria-busy={mutation.isPending}>
        <label htmlFor="order-customer">Customer</label>
        <select id="order-customer" {...form.register('customerId')} onChange={(event) => {
          form.setValue('customerId', event.target.value);
          form.setValue('currencyCode', '');
        }}>
          <option value="">Select a customer</option>
          {customerQuery.data?.items.map((customer) => <option key={customer.id} value={customer.id}>{customer.name}</option>)}
        </select>
        {form.formState.errors.customerId && <p>{form.formState.errors.customerId.message}</p>}

        <label htmlFor="order-currency">Currency</label>
        <select id="order-currency" {...form.register('currencyCode')} disabled={!selectedCustomer}>
          <option value="">Select a currency</option>
          {currencies.map((currency) => <option key={currency} value={currency}>{currency}</option>)}
        </select>
        {form.formState.errors.currencyCode && <p>{form.formState.errors.currencyCode.message}</p>}

        <h3>Line items</h3>
        {fields.map((field, index) => (
          <fieldset key={field.id}>
            <legend>Line item {index + 1}</legend>
            <label htmlFor={`sku-${index}`}>Product SKU</label>
            <input id={`sku-${index}`} {...form.register(`lineItems.${index}.productSku`)} />
            {form.formState.errors.lineItems?.[index]?.productSku && <p>{form.formState.errors.lineItems[index]?.productSku?.message}</p>}
            <label htmlFor={`quantity-${index}`}>Quantity</label>
            <input id={`quantity-${index}`} type="number" min="1" {...form.register(`lineItems.${index}.quantity`, { valueAsNumber: true })} />
            {form.formState.errors.lineItems?.[index]?.quantity && <p>{form.formState.errors.lineItems[index]?.quantity?.message}</p>}
            <label htmlFor={`price-${index}`}>Unit price</label>
            <input id={`price-${index}`} type="number" min="0" step="0.01" {...form.register(`lineItems.${index}.unitPrice`, { valueAsNumber: true })} />
            {form.formState.errors.lineItems?.[index]?.unitPrice && <p>{form.formState.errors.lineItems[index]?.unitPrice?.message}</p>}
            <button type="button" onClick={() => remove(index)} disabled={fields.length === 1}>Remove</button>
          </fieldset>
        ))}
        {form.formState.errors.lineItems?.root && <p>{form.formState.errors.lineItems.root.message}</p>}
        <button type="button" onClick={() => append({ productSku: '', quantity: 1, unitPrice: 0 })}>Add line item</button>
        <p><strong>Estimated subtotal:</strong> {selectedCustomer ? `${estimatedTotal.toFixed(2)} ${form.watch('currencyCode') || ''}` : `${estimatedTotal.toFixed(2)}`}</p>
        {mutation.error && <p role={mutation.error instanceof ApiError && mutation.error.status === 409 ? 'alert' : 'alert'}>{mutation.error.message}</p>}
        <button type="submit" disabled={mutation.isPending}>Create order</button>
      </form>
    </section>
  );
}
