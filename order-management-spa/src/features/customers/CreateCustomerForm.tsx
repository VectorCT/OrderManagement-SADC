import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useForm } from 'react-hook-form';
import { ApiError } from '../../api/client';
import { createCustomer } from './api';
import { customerSchema, SADC_COUNTRIES, type CustomerFormData } from './schema';
import styles from '../workspace.module.css';

export function CreateCustomerForm({ onCreated }: { onCreated: (message: string) => void }) {
  const queryClient = useQueryClient();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<CustomerFormData>({
    resolver: zodResolver(customerSchema),
    defaultValues: { name: '', email: '', countryCode: 'ZA' },
  });

  const mutation = useMutation({
    mutationFn: createCustomer,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['customers'] });
      onCreated('Customer created successfully.');
    },
  });

  async function submit(values: CustomerFormData) {
    try {
      await mutation.mutateAsync(values);
    } catch (error) {
      if (error instanceof ApiError && error.details.errors) {
        for (const [field, messages] of Object.entries(error.details.errors)) {
          if (field in values) {
            setError(field as keyof CustomerFormData, { message: messages[0] });
          }
        }
      }
    }
  }

  return (
    <form className={styles.customerForm} onSubmit={handleSubmit(submit)} noValidate aria-busy={isSubmitting}>
      <div className={styles.formField}>
        <label htmlFor="customer-name">Name</label>
        <input id="customer-name" {...register('name')} aria-invalid={!!errors.name} aria-describedby={errors.name ? 'customer-name-error' : undefined} />
        {errors.name && <span className={styles.fieldError} id="customer-name-error">{errors.name.message}</span>}
      </div>
      <div className={styles.formField}>
        <label htmlFor="customer-email">Email</label>
        <input id="customer-email" type="email" {...register('email')} aria-invalid={!!errors.email} aria-describedby={errors.email ? 'customer-email-error' : undefined} />
        {errors.email && <span className={styles.fieldError} id="customer-email-error">{errors.email.message}</span>}
      </div>
      <div className={styles.formField}>
        <label htmlFor="customer-country">Country</label>
        <select id="customer-country" {...register('countryCode')} aria-invalid={!!errors.countryCode} aria-describedby={errors.countryCode ? 'customer-country-error' : undefined}>
          {SADC_COUNTRIES.map(([code, name]) => <option key={code} value={code}>{name}</option>)}
        </select>
        {errors.countryCode && <span className={styles.fieldError} id="customer-country-error">{errors.countryCode.message}</span>}
      </div>
      {mutation.error instanceof ApiError && !mutation.error.details.errors && (
        <p className={styles.fieldError} role="alert">{mutation.error.message}</p>
      )}
      <button className={styles.primaryButton} type="submit" disabled={isSubmitting}>Create customer</button>
    </form>
  );
}
