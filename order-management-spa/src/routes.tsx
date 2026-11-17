import { createBrowserRouter } from 'react-router-dom';
import App from './App';
import { RouteGuard } from './auth/RouteGuard';
import { LoginPage } from './auth/LoginPage';
import { RegisterPage } from './auth/RegisterPage';
import { CustomersPage } from './features/customers/CustomersPage';
import { OrdersPage } from './features/orders/OrdersPage';
import { OrderDetailPage } from './features/orders/OrderDetailPage';
import { CreateOrderPage } from './features/orders/CreateOrderPage';
import { CreateCustomerPage } from './features/customers/CreateCustomerPage';
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
      { path: '/register', element: <RegisterPage /> },
      {
        element: <RouteGuard />,
        children: [
          { path: '/', element: <div>Customers and orders</div> },
          { path: '/customers', element: <CustomersPage /> },
          { path: '/customers/new', element: <CreateCustomerPage /> },
          { path: '/orders', element: <OrdersPage /> },
          { path: '/orders/new', element: <CreateOrderPage /> },
          { path: '/orders/:id', element: <OrderDetailPage /> },
        ],
      },
      { path: '*', element: <div>Page not found</div> },
    ],
  },
]);
