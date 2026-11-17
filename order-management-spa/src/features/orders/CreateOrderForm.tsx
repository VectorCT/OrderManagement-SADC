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
      <form className={styles.orderForm} onSubmit={form.handleSubmit(submit)} noValidate aria-busy={mutation.isPending}>
        <div className={styles.orderFields}>
          <div className={styles.formField}>
            <label htmlFor="order-customer">Customer</label>
            <select id="order-customer" {...form.register('customerId')} onChange={(event) => {
              form.setValue('customerId', event.target.value, { shouldValidate: true });
              form.setValue('currencyCode', '', { shouldValidate: true });
            }} aria-invalid={!!form.formState.errors.customerId} aria-describedby={form.formState.errors.customerId ? 'order-customer-error' : undefined}>
              <option value="">Select a customer</option>
              {customerQuery.data?.items.map((customer) => <option key={customer.id} value={customer.id}>{customer.name}</option>)}
            </select>
            {form.formState.errors.customerId && <p className={styles.fieldError} id="order-customer-error">{form.formState.errors.customerId.message}</p>}
          </div>
          <div className={styles.formField}>
            <label htmlFor="order-currency">Currency</label>
            <select id="order-currency" {...form.register('currencyCode')} disabled={!selectedCustomer} aria-invalid={!!form.formState.errors.currencyCode} aria-describedby={form.formState.errors.currencyCode ? 'order-currency-error' : undefined}>
              <option value="">Select a currency</option>
              {currencies.map((currency) => <option key={currency} value={currency}>{currency}</option>)}
            </select>
            {form.formState.errors.currencyCode && <p className={styles.fieldError} id="order-currency-error">{form.formState.errors.currencyCode.message}</p>}
          </div>
        </div>
        <h3>Line items</h3>
        {fields.map((field, index) => (
          <fieldset key={field.id}>
            <legend>Line item {index + 1}</legend>
            <div className={styles.lineItemGrid}>
              <div className={styles.formField}>
                <label htmlFor={`sku-${index}`}>Product SKU</label>
                <input id={`sku-${index}`} {...form.register(`lineItems.${index}.productSku`)} aria-invalid={!!form.formState.errors.lineItems?.[index]?.productSku} />
                {form.formState.errors.lineItems?.[index]?.productSku && <p className={styles.fieldError}>{form.formState.errors.lineItems[index]?.productSku?.message}</p>}
              </div>
              <div className={styles.formField}>
                <label htmlFor={`quantity-${index}`}>Quantity</label>
                <input id={`quantity-${index}`} type="number" min="1" {...form.register(`lineItems.${index}.quantity`, { valueAsNumber: true })} aria-invalid={!!form.formState.errors.lineItems?.[index]?.quantity} />
                {form.formState.errors.lineItems?.[index]?.quantity && <p className={styles.fieldError}>{form.formState.errors.lineItems[index]?.quantity?.message}</p>}
              </div>
              <div className={styles.formField}>
                <label htmlFor={`price-${index}`}>Unit price</label>
                <input id={`price-${index}`} type="number" min="0" step="0.01" {...form.register(`lineItems.${index}.unitPrice`, { valueAsNumber: true })} aria-invalid={!!form.formState.errors.lineItems?.[index]?.unitPrice} />
                {form.formState.errors.lineItems?.[index]?.unitPrice && <p className={styles.fieldError}>{form.formState.errors.lineItems[index]?.unitPrice?.message}</p>}
              </div>
              <button className={styles.secondaryButton} type="button" onClick={() => remove(index)} disabled={fields.length === 1}>Remove</button>
            </div>
          </fieldset>
        ))}
        {form.formState.errors.lineItems?.root && <p className={styles.fieldError} role="alert">{form.formState.errors.lineItems.root.message}</p>}
        <button className={styles.secondaryButton} type="button" onClick={() => append({ productSku: '', quantity: 1, unitPrice: 0 })}>Add line item</button>
        <div className={styles.orderSummary}>
          <h3>Summary</h3>
          <p>Line items <strong>{fields.length}</strong></p>
          <p>Estimated total <strong>{selectedCustomer ? `${form.watch('currencyCode') || ''} ${estimatedTotal.toFixed(2)}` : estimatedTotal.toFixed(2)}</strong></p>
          <small>Estimate only. Final total is calculated by the server.</small>
        </div>
        {mutation.error && <p className={styles.fieldError} role="alert">{mutation.error.message}</p>}
        <button className={styles.primaryButton} type="submit" disabled={mutation.isPending}>{mutation.isPending ? 'Creating order...' : 'Create order'}</button>
      </form>
    </section>
  );
}
