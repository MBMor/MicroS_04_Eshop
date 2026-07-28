import {
    render,
    screen,
    waitFor,
} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import {
    beforeEach,
    describe,
    expect,
    it,
    vi,
} from 'vitest';

import { getBasket } from '../api/basketApi';
import { createOrder } from '../api/ordersApi';
import { CheckoutPage } from './CheckoutPage';

const navigateMock = vi.fn();

vi.mock('../api/basketApi', () => ({
    getBasket: vi.fn(),
}));

vi.mock('../api/ordersApi', () => ({
    createOrder: vi.fn(),
}));

vi.mock('react-router', async importOriginal => {
    const actual =
        await importOriginal<typeof import('react-router')>();

    return {
        ...actual,
        useNavigate: () => navigateMock,
    };
});

const mockedGetBasket =
    vi.mocked(getBasket);

const mockedCreateOrder =
    vi.mocked(createOrder);

describe('CheckoutPage idempotency', () => {
    beforeEach(() => {
        navigateMock.mockReset();
        mockedGetBasket.mockReset();
        mockedCreateOrder.mockReset();

        mockedGetBasket.mockResolvedValue({
            items: [
                {
                    productId: 'product-1',
                    productName: 'Keyboard',
                    unitPrice: 100,
                    currency: 'CZK',
                    quantity: 1,
                    lineTotal: 100,
                },
            ],
            totals: [
                {
                    currency: 'CZK',
                    amount: 100,
                },
            ],
            updatedAtUtc: '2026-07-28T00:00:00Z',
            expiresAtUtc: '2026-07-28T01:00:00Z',
        });
    });

    it('reuses the key after a retryable transport failure',
        async () => {
            const user = userEvent.setup();

            mockedCreateOrder
                .mockRejectedValueOnce(
                    new TypeError('Network unavailable'),
                )
                .mockResolvedValueOnce(
                    createOrderResponse('order-1'),
                );

            renderCheckoutPage();

            const submit = await screen.findByRole(
                'button',
                { name: 'Create order' },
            );

            await user.click(submit);

            await waitFor(() => {
                expect(mockedCreateOrder).toHaveBeenCalledTimes(1);
            });

            const firstKey =
                mockedCreateOrder.mock.calls[0][1];

            await user.click(submit);

            await waitFor(() => {
                expect(mockedCreateOrder).toHaveBeenCalledTimes(2);
            });

            expect(
                mockedCreateOrder.mock.calls[1][1],
            ).toBe(firstKey);

            expect(navigateMock).toHaveBeenCalledWith(
                '/orders/order-1',
            );
        });

    it('creates a new key after the checkout input changes',
        async () => {
            const user = userEvent.setup();

            mockedCreateOrder
                .mockRejectedValueOnce(
                    new TypeError('Network unavailable'),
                )
                .mockResolvedValueOnce(
                    createOrderResponse('order-2'),
                );

            renderCheckoutPage();

            const submit = await screen.findByRole(
                'button',
                { name: 'Create order' },
            );

            await user.click(submit);

            await waitFor(() => {
                expect(mockedCreateOrder).toHaveBeenCalledTimes(1);
            });

            const firstKey =
                mockedCreateOrder.mock.calls[0][1];

            const email = screen.getByRole('textbox');

            await user.clear(email);
            await user.type(email, 'changed@example.com');
            await user.click(submit);

            await waitFor(() => {
                expect(mockedCreateOrder).toHaveBeenCalledTimes(2);
            });

            expect(
                mockedCreateOrder.mock.calls[1][1],
            ).not.toBe(firstKey);
        });
});

function renderCheckoutPage() {
    render(
        <MemoryRouter>
            <CheckoutPage />
        </MemoryRouter>,
    );
}

function createOrderResponse(id: string) {
    return {
        id,
        customerEmail: 'alice@example.com',
        status: 'PendingStockReservation',
        totalAmount: 100,
        currency: 'CZK',
        paymentMethod: 'test-success',
        createdAtUtc: '2026-07-28T00:00:00Z',
        updatedAtUtc: null,
        items: [],
    };
}
