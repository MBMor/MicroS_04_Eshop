import { apiRequest } from './apiClient';
import type {
    CreateOrderRequest,
    Order,
    OrderSummary,
} from '../types/order';

const orderRequestOptions = {
    includeDevelopmentCustomerId: true,
} as const;

export function createOrder(
    request: CreateOrderRequest,
): Promise<Order> {
    return apiRequest<Order>(
        '/api/v1/orders',
        {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(request),
        },
        orderRequestOptions,
    );
}

export function getOrder(orderId: string): Promise<Order> {
    return apiRequest<Order>(
        `/api/v1/orders/${orderId}`,
        {},
        orderRequestOptions,
    );
}

export function getOrders(): Promise<OrderSummary[]> {
    return apiRequest<OrderSummary[]>(
        '/api/v1/orders',
        {},
        orderRequestOptions,
    );
}
