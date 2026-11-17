import { createBrowserRouter } from 'react-router-dom';
import App from './App';
import { RouteGuard } from './auth/RouteGuard';
import { LoginPage } from './auth/LoginPage';
import { CustomersPage } from './features/customers/CustomersPage';
import { RouteErrorBoundary } from './components/RouteErrorBoundary';

export const router = createBrowserRouter([
  {
    element: (
      <RouteErrorBoundary>
        <App />
      </RouteErrorBoundary>
    ),
    children: [
      { path: '/login', element: <LoginPage /> },
      {
        element: <RouteGuard />,
        children: [
          { path: '/', element: <div>Customers and orders</div> },
          { path: '/customers', element: <CustomersPage /> },
          { path: '/orders', element: <div>Orders</div> },
          { path: '/orders/:id', element: <div>Order details</div> },
        ],
      },
      { path: '*', element: <div>Page not found</div> },
    ],
  },
]);
