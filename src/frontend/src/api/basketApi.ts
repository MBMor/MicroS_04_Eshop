import type { Basket } from '../types/basket';
import { apiRequest } from './apiClient';

export function getBasket(): Promise<Basket> {
    return apiRequest<Basket>(
        '/api/v1/basket',
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
    );
}

export function clearBasket(): Promise<void> {
    return apiRequest<void>(
        '/api/v1/basket',
        {
            method: 'DELETE',
        },
    );
}
