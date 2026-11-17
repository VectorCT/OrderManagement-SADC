import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { searchCustomers } from './api';
import { CreateCustomerForm } from './CreateCustomerForm';

export function CustomersPage() {
  const [params, setParams] = useSearchParams();
  const initialSearch = params.get('search') ?? '';
  const [searchInput, setSearchInput] = useState(initialSearch);
  const [debouncedSearch, setDebouncedSearch] = useState(initialSearch);
  const page = Math.max(1, Number(params.get('page') ?? 1) || 1);
  const pageSize = Math.min(100, Math.max(1, Number(params.get('pageSize') ?? 20) || 20));
  const [feedback, setFeedback] = useState('');

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(searchInput), 300);
    return () => window.clearTimeout(timeout);
  }, [searchInput]);

  useEffect(() => {
    setParams((current) => {
      if (debouncedSearch) current.set('search', debouncedSearch);
      else current.delete('search');
      current.set('page', String(page));
      current.set('pageSize', String(pageSize));
      return current;
    }, { replace: true });
  }, [debouncedSearch, page, pageSize, setParams]);

  const query = useQuery({
    queryKey: ['customers', { search: debouncedSearch, page, pageSize }],
    queryFn: () => searchCustomers(debouncedSearch, page, pageSize),
  });

  function changePage(nextPage: number) {
    setParams((current) => {
      current.set('page', String(nextPage));
      return current;
    });
  }

  function changePageSize(nextSize: number) {
    setParams((current) => {
      current.set('page', '1');
      current.set('pageSize', String(Math.min(nextSize, 100)));
      return current;
    });
  }

  return (
    <section aria-labelledby="customers-heading">
      <h1 id="customers-heading">Customers</h1>
      <div role="status" aria-live="polite">{feedback}</div>

      <CreateCustomerForm onCreated={setFeedback} />

      <label htmlFor="customer-search">Search customers</label>
      <input id="customer-search" value={searchInput} onChange={(event) => setSearchInput(event.target.value)} />
      <label htmlFor="customer-page-size">Page size</label>
      <select id="customer-page-size" value={pageSize} onChange={(event) => changePageSize(Number(event.target.value))}>
        {[10, 20, 50, 100].map((size) => <option key={size} value={size}>{size}</option>)}
      </select>

      {query.isLoading && <p role="status" aria-busy="true">Loading customers...</p>}
      {query.isError && (
        <div role="alert">
          <p>{query.error instanceof Error ? query.error.message : 'Unable to load customers.'}</p>
          <button type="button" onClick={() => void query.refetch()}>Retry</button>
        </div>
      )}
      {query.isSuccess && query.data.items.length === 0 && <p>No customers found.</p>}
      {query.isSuccess && query.data.items.length > 0 && (
        <>
          <table>
            <caption>Customer search results</caption>
            <thead><tr><th scope="col">Name</th><th scope="col">Email</th><th scope="col">Country</th></tr></thead>
            <tbody>
              {query.data.items.map((customer) => (
                <tr key={customer.id}><td>{customer.name}</td><td>{customer.email}</td><td>{customer.countryCode}</td></tr>
              ))}
            </tbody>
          </table>
          <nav aria-label="Customer pagination">
            <button type="button" disabled={page <= 1} onClick={() => changePage(page - 1)}>Previous</button>
            <span>Page {page}</span>
            <button type="button" disabled={query.data.items.length < pageSize} onClick={() => changePage(page + 1)}>Next</button>
          </nav>
        </>
      )}
    </section>
  );
}
