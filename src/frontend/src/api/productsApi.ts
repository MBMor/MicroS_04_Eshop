import { buildApiUrl } from '../config/apiConfig';
import type { Product } from '../types/product';

export class ApiError extends Error {
    public readonly status: number;

    public constructor(message: string, status: number) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
    }
}

export async function getProducts(): Promise<Product[]> {
    const response = await fetch(buildApiUrl('/api/v1/products'), {
        method: 'GET',
        headers: {
            Accept: 'application/json',
        },
    });

    if (!response.ok) {
        throw new ApiError('Failed to load products.', response.status);
    }

    return await response.json() as Product[];
}
