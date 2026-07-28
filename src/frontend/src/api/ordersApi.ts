import type {
    CreateOrderRequest,
    Order,
    OrderSummary,
} from '../types/order';
import { apiRequest } from './apiClient';

export function createOrder(
    request: CreateOrderRequest,
    idempotencyKey: string,
): Promise<Order> {
    return apiRequest<Order>(
        '/api/v1/orders',
        {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Idempotency-Key': idempotencyKey,
            },
            body: JSON.stringify(request),
        },
    );
}

export function getOrder(
    orderId: string,
): Promise<Order> {
    return apiRequest<Order>(
        `/api/v1/orders/${orderId}`,
    );
}

export function getOrders():
    Promise<OrderSummary[]> {
    return apiRequest<OrderSummary[]>(
        '/api/v1/orders',
    );
}
