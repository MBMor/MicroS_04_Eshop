import { apiRequest } from './apiClient';
import type { Basket } from '../types/basket';

const basketRequestOptions = {
    includeDevelopmentCustomerId: true,
} as const;

export function getBasket(): Promise<Basket> {
    return apiRequest<Basket>(
        '/api/v1/basket',
        {},
        basketRequestOptions,
    );
}

export function addBasketItem(
    productId: string,
    quantity: number,
): Promise<Basket> {
    return apiRequest<Basket>(
        '/api/v1/basket/items',
        {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                productId,
                quantity,
            }),
        },
        basketRequestOptions,
    );
}

export function updateBasketItem(
    productId: string,
    quantity: number,
): Promise<Basket> {
    return apiRequest<Basket>(
        `/api/v1/basket/items/${productId}`,
        {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                quantity,
            }),
        },
        basketRequestOptions,
    );
}

export function removeBasketItem(
    productId: string,
): Promise<void> {
    return apiRequest<void>(
        `/api/v1/basket/items/${productId}`,
        {
            method: 'DELETE',
        },
        basketRequestOptions,
    );
}

export function clearBasket(): Promise<void> {
    return apiRequest<void>(
        '/api/v1/basket',
        {
            method: 'DELETE',
        },
        basketRequestOptions,
    );
}
