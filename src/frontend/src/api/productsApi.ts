import { apiRequest } from './apiClient';
import type { Product } from '../types/product';

export function getProducts(): Promise<Product[]> {
    return apiRequest<Product[]>('/api/v1/products');
}
