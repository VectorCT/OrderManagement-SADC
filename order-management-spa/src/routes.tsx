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
import { AdminRouteGuard } from './auth/AdminRouteGuard';
import { AdminUsersPage } from './features/admin/AdminUsersPage';
import { AdminUserFormPage } from './features/admin/AdminUserFormPage';
import { MessagingDiagnosticsPage } from './features/admin/MessagingDiagnosticsPage';

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
          {
            element: <AdminRouteGuard />,
            children: [
              { path: '/admin/users', element: <AdminUsersPage /> },
              { path: '/admin/users/new', element: <AdminUserFormPage /> },
              { path: '/admin/users/:userId/edit', element: <AdminUserFormPage /> },
              { path: '/admin/messaging', element: <MessagingDiagnosticsPage /> },
            ],
          },
        ],
      },
      { path: '*', element: <div>Page not found</div> },
    ],
  },
]);
