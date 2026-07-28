import {
    beforeEach,
    describe,
    expect,
    it,
    vi,
} from 'vitest';

import { apiRequest } from './apiClient';
import { createOrder } from './ordersApi';

vi.mock('./apiClient', () => ({
    apiRequest: vi.fn(),
}));

const mockedApiRequest =
    vi.mocked(apiRequest);

describe('createOrder', () => {
    beforeEach(() => {
        mockedApiRequest.mockReset();
    });

    it('sends the checkout idempotency key', async () => {
        mockedApiRequest.mockResolvedValue({
            id: 'order-1',
        });

        await createOrder(
            {
                customerEmail: 'alice@example.com',
                paymentMethod: 'test-success',
            },
            'checkout-key-1',
        );

        expect(mockedApiRequest).toHaveBeenCalledWith(
            '/api/v1/orders',
            expect.objectContaining({
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Idempotency-Key': 'checkout-key-1',
                },
            }),
        );
    });
});
