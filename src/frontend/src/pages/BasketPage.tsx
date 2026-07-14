import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';

import {
    clearBasket,
    getBasket,
    removeBasketItem,
    updateBasketItem,
} from '../api/basketApi';
import type { Basket } from '../types/basket';

export function BasketPage() {
    const [basket, setBasket] = useState<Basket | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [pendingAction, setPendingAction] = useState<string | null>(null);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);


    async function loadBasket() {
        setIsLoading(true);
        setErrorMessage(null);

        try {
            const loadedBasket = await getBasket();
            setBasket(loadedBasket);
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setIsLoading(false);
        }
    }

    useEffect(() => {
        let isCancelled = false;

        async function initializeBasket() {
            try {
                const loadedBasket = await getBasket();

                if (!isCancelled) {
                    setBasket(loadedBasket);
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

        void initializeBasket();

        return () => {
            isCancelled = true;
        };
    }, []);

    async function changeQuantity(
        productId: string,
        quantity: number,
    ) {
        setPendingAction(productId);
        setErrorMessage(null);

        try {
            setBasket(await updateBasketItem(productId, quantity));
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setPendingAction(null);
        }
    }

    async function removeItem(productId: string) {
        setPendingAction(productId);
        setErrorMessage(null);

        try {
            await removeBasketItem(productId);
            await loadBasket();
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setPendingAction(null);
        }
    }

    async function handleClearBasket() {
        setPendingAction('clear');
        setErrorMessage(null);

        try {
            await clearBasket();
            await loadBasket();
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setPendingAction(null);
        }
    }

    if (isLoading) {
        return (
            <main className="app-shell">
                <section className="state-card">
                    <h1>Loading basket</h1>
                    <p>Waiting for Basket Service response.</p>
                </section>
            </main>
        );
    }

    return (
        <main className="app-shell">
            <section className="page-header">
                <div>
                    <p className="eyebrow">Eshop Capstone</p>
                    <h1>Basket</h1>
                    <p className="description">
                        Basket data is stored temporarily in Redis.
                    </p>
                </div>

                <Link className="secondary-button link-button" to="/">
                    Continue shopping
                </Link>
            </section>

            {errorMessage && (
                <section className="state-card error-card">
                    <h2>Basket operation failed</h2>
                    <p>{errorMessage}</p>
                </section>
            )}

            {basket && basket.items.length === 0 && (
                <section className="state-card">
                    <h2>Your basket is empty</h2>
                    <p>Add a product from the catalog.</p>

                    <Link className="primary-button link-button" to="/">
                        Open product catalog
                    </Link>
                </section>
            )}

            {basket && basket.items.length > 0 && (
                <div className="basket-layout">
                    <section className="basket-items" aria-label="Basket items">
                        {basket.items.map(item => {
                            const isPending = pendingAction === item.productId;

                            return (
                                <article className="basket-item" key={item.productId}>
                                    <div>
                                        <h2>{item.productName}</h2>
                                        <p className="basket-unit-price">
                                            {formatMoney(item.unitPrice, item.currency)} each
                                        </p>
                                    </div>

                                    <div className="quantity-controls">
                                        <button
                                            type="button"
                                            aria-label={`Decrease ${item.productName} quantity`}
                                            disabled={isPending || item.quantity <= 1}
                                            onClick={() => {
                                                void changeQuantity(
                                                    item.productId,
                                                    item.quantity - 1,
                                                );
                                            }}
                                        >
                                            −
                                        </button>

                                        <span>{item.quantity}</span>

                                        <button
                                            type="button"
                                            aria-label={`Increase ${item.productName} quantity`}
                                            disabled={isPending || item.quantity >= 100}
                                            onClick={() => {
                                                void changeQuantity(
                                                    item.productId,
                                                    item.quantity + 1,
                                                );
                                            }}
                                        >
                                            +
                                        </button>
                                    </div>

                                    <strong>
                                        {formatMoney(item.lineTotal, item.currency)}
                                    </strong>

                                    <button
                                        className="danger-button"
                                        type="button"
                                        disabled={isPending}
                                        onClick={() => {
                                            void removeItem(item.productId);
                                        }}
                                    >
                                        Remove
                                    </button>
                                </article>
                            );
                        })}
                    </section>

                    <aside className="basket-summary">
                        <h2>Summary</h2>

                        {basket.totals.map(total => (
                            <div className="summary-row" key={total.currency}>
                                <span>Total {total.currency}</span>
                                <strong>
                                    {formatMoney(total.amount, total.currency)}
                                </strong>
                            </div>
                        ))}

                        <p className="basket-expiration">
                            Basket expires at{' '}
                            {new Date(basket.expiresAtUtc).toLocaleString('cs-CZ')}.
                        </p>

                        <button
                            className="danger-button full-width-button"
                            type="button"
                            disabled={pendingAction === 'clear'}
                            onClick={() => {
                                void handleClearBasket();
                            }}
                        >
                            Clear basket
                        </button>
                    </aside>
                </div>
            )}
        </main>
    );
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
        : 'Unexpected basket error.';
}
