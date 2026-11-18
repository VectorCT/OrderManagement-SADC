export interface ApiResponse<T = unknown> {
  code: number;
  message: string;
  results: T | null;
  endpoint: string;
}

export interface ProblemDetailsError {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}

export interface Customer {
  id: string;
  name: string;
  email: string;
  countryCode: string;
  createdAt: string;
}

export interface OrderLineItem {
  id: string;
  orderId: string;
  productSku: string;
  quantity: number;
  unitPrice: number;
}

export interface OrderSummary {
  id: string;
  customerId: string;
  status: 'Pending' | 'Paid' | 'Fulfilled' | 'Cancelled';
  createdAt: string;
  currencyCode: string;
  totalAmount: number;
}

export interface OrderDetail extends OrderSummary {
  lineItems: OrderLineItem[];
  rowVersion: string;
  notes: string | null;
  updatedAt: string | null;
}

export interface AdminUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  createdAt: string;
  roles: string[];
}

export interface MessagingDiagnostics {
  generatedAt: string;
  ordersCreatedLast24Hours: number;
  ordersByStatus: { status: string; count: number }[];
  outbox: {
    pendingCount: number;
    retryScheduledCount: number;
    publishedCount: number;
    recentMessages: { messageId: string; type: string; createdAt: string; publishedAt: string | null; attempts: number; lastError: string | null }[];
  };
  processedMessageCount: number;
  recentProcessedMessages: { messageId: string; consumer: string; processedAt: string }[];
  rabbitMq: {
    isReachable: boolean;
    error: string | null;
    queues: { name: string; exists: boolean; readyMessages: number | null; consumers: number | null }[];
  };
}

