import { useEffect, useState } from 'react';
import { Link } from 'react-router';

import { getOrders } from '../api/ordersApi';
import type { OrderSummary } from '../types/order';

export function OrdersPage() {
    const [orders, setOrders] = useState<OrderSummary[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] =
        useState<string | null>(null);

    async function loadOrders() {
        setIsLoading(true);
        setErrorMessage(null);

        try {
            const loadedOrders = await getOrders();
            setOrders(loadedOrders);
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setIsLoading(false);
        }
    }

    useEffect(() => {
        let isCancelled = false;

        async function loadInitialOrders() {
            try {
                const loadedOrders = await getOrders();

                if (!isCancelled) {
                    setOrders(loadedOrders);
                }
            } catch (error) {
                if (!isCancelled) {
                    setErrorMessage(getErrorMessage(error));
                }
            } finally {
                if (!isCancelled) {
                    setIsLoading(false);
                }
            }
        }

        void loadInitialOrders();

        return () => {
            isCancelled = true;
        };
    }, []);

    return (
        <main className="app-shell">
            <section className="page-header">
                <div>
                    <p className="eyebrow">Eshop Capstone</p>
                    <h1>Orders</h1>
                    <p className="description">
                        Durable orders stored by Orders Service.
                    </p>
                </div>

                <button
                    className="secondary-button"
                    type="button"
                    onClick={() => {
                        void loadOrders();
                    }}
                >
                    Refresh orders
                </button>
            </section>

            {isLoading && (
                <section className="state-card">
                    <h2>Loading orders</h2>
                </section>
            )}

            {!isLoading && errorMessage && (
                <section className="state-card error-card">
                    <h2>Orders could not be loaded</h2>
                    <p>{errorMessage}</p>
                </section>
            )}

            {!isLoading
                && !errorMessage
                && orders.length === 0 && (
                    <section className="state-card">
                        <h2>No orders yet</h2>
                        <p>Create an order from the basket checkout.</p>
                    </section>
                )}

            {!isLoading
                && !errorMessage
                && orders.length > 0 && (
                    <section className="order-list" aria-label="Orders">
                        {orders.map(order => (
                            <article className="order-card" key={order.id}>
                                <div>
                                    <p className="order-id">
                                        {order.id}
                                    </p>

                                    <h2>
                                        {formatMoney(
                                            order.totalAmount,
                                            order.currency,
                                        )}
                                    </h2>

                                    <p>
                                        {order.itemCount} item(s) ·{' '}
                                        {new Date(
                                            order.createdAtUtc,
                                        ).toLocaleString('cs-CZ')}
                                    </p>
                                </div>

                                <div className="order-card-actions">
                                    <span className={getStatusClass(order.status)}>
                                        {order.status}
                                    </span>

                                    <Link
                                        className="secondary-button link-button"
                                        to={`/orders/${order.id}`}
                                    >
                                        Open order
                                    </Link>
                                </div>
                            </article>
                        ))}
                    </section>
                )}
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
