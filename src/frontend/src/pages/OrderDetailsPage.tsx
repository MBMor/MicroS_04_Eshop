import {
    useCallback,
    useEffect,
    useRef,
    useState,
} from 'react';
import {
    Link,
    useParams,
} from 'react-router';

import { getOrder } from '../api/ordersApi';
import type { Order } from '../types/order';

const terminalStatuses = new Set([
    'StockReservationFailed',
    'PaymentFailed',
    'Confirmed',
    'Cancelled',
]);

const maxPollingAttempts = 10;
const pollingIntervalMilliseconds = 3_000;

export function OrderDetailsPage() {
    const { orderId } = useParams();

    const [order, setOrder] = useState<Order | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [errorMessage, setErrorMessage] =
        useState<string | null>(null);

    const pollingAttempt = useRef(0);

    const loadOrder = useCallback(
        async (showInitialLoading: boolean) => {
            if (!orderId) {
                setErrorMessage('Order id is missing.');
                setIsLoading(false);
                return null;
            }

            if (showInitialLoading) {
                setIsLoading(true);
            } else {
                setIsRefreshing(true);
            }

            setErrorMessage(null);

            try {
                const loadedOrder = await getOrder(orderId);
                setOrder(loadedOrder);

                return loadedOrder;
            } catch (error) {
                setErrorMessage(getErrorMessage(error));

                return null;
            } finally {
                setIsLoading(false);
                setIsRefreshing(false);
            }
        },
        [orderId],
    );

    useEffect(() => {
        let timeoutId: number | undefined;
        let disposed = false;

        pollingAttempt.current = 0;

        async function pollOrder() {
            const loadedOrder = await loadOrder(
                pollingAttempt.current === 0,
            );

            if (disposed || !loadedOrder) {
                return;
            }

            pollingAttempt.current += 1;

            if (
                !terminalStatuses.has(loadedOrder.status)
                && pollingAttempt.current < maxPollingAttempts
            ) {
                timeoutId = window.setTimeout(
                    () => {
                        void pollOrder();
                    },
                    pollingIntervalMilliseconds,
                );
            }
        }

        void pollOrder();

        return () => {
            disposed = true;

            if (timeoutId !== undefined) {
                window.clearTimeout(timeoutId);
            }
        };
    }, [loadOrder]);

    if (isLoading) {
        return (
            <main className="app-shell">
                <section className="state-card">
                    <h1>Loading order</h1>
                </section>
            </main>
        );
    }

    if (!order) {
        return (
            <main className="app-shell">
                <section className="state-card error-card">
                    <h1>Order is unavailable</h1>
                    <p>{errorMessage}</p>
                </section>
            </main>
        );
    }

    return (
        <main className="app-shell">
            <section className="page-header">
                <div>
                    <p className="eyebrow">Order status</p>
                    <h1>{formatMoney(order.totalAmount, order.currency)}</h1>
                    <p className="order-id">{order.id}</p>
                </div>

                <div className="header-actions">
                    <button
                        className="secondary-button"
                        type="button"
                        disabled={isRefreshing}
                        onClick={() => {
                            void loadOrder(false);
                        }}
                    >
                        {isRefreshing ? 'Refreshing…' : 'Refresh status'}
                    </button>

                    <Link className="secondary-button link-button" to="/orders">
                        All orders
                    </Link>
                </div>
            </section>

            {errorMessage && (
                <section className="state-card error-card">
                    <p>{errorMessage}</p>
                </section>
            )}

            <section className="order-details-layout">
                <article className="order-status-card">
                    <h2>Current status</h2>

                    <span className={getStatusClass(order.status)}>
                        {order.status}
                    </span>

                    {order.status === 'PendingStockReservation' && (
                        <div className="pending-note">
                            <strong>Waiting for stock reservation</strong>

                            <p>
                                The durable order exists. Inventory event processing
                                will be implemented in later messaging steps.
                            </p>
                        </div>
                    )}

                    {order.status === 'PendingPayment' && (
                        <div className="pending-note">
                            <strong>Waiting for fake payment authorization</strong>
                        </div>
                    )}

                    <dl className="order-metadata">
                        <div>
                            <dt>Created</dt>
                            <dd>
                                {new Date(
                                    order.createdAtUtc,
                                ).toLocaleString('cs-CZ')}
                            </dd>
                        </div>

                        <div>
                            <dt>Customer email</dt>
                            <dd>{order.customerEmail}</dd>
                        </div>

                        <div>
                            <dt>Payment method</dt>
                            <dd>{order.paymentMethod}</dd>
                        </div>
                    </dl>
                </article>

                <article className="order-items-card">
                    <h2>Items</h2>

                    {order.items.map(item => (
                        <div className="order-detail-item" key={item.id}>
                            <div>
                                <strong>{item.productName}</strong>

                                <span>
                                    {item.quantity} ×{' '}
                                    {formatMoney(item.unitPrice, item.currency)}
                                </span>
                            </div>

                            <strong>
                                {formatMoney(item.lineTotal, item.currency)}
                            </strong>
                        </div>
                    ))}

                    <div className="checkout-total">
                        <span>Total</span>
                        <strong>
                            {formatMoney(order.totalAmount, order.currency)}
                        </strong>
                    </div>
                </article>
            </section>
        </main>
    );
}

function getStatusClass(status: string): string {
    return `status-badge status-${status.toLowerCase()}`;
}

function formatMoney(
    amount: number,
    currency: string,
): string {
    return amount.toLocaleString('cs-CZ', {
        style: 'currency',
        currency,
    });
}

function getErrorMessage(error: unknown): string {
    return error instanceof Error
        ? error.message
        : 'Unexpected order error.';
}
