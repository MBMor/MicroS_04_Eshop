import {
    type SubmitEvent,
    useEffect,
    useState,
} from 'react';
import {
    Link,
    useNavigate,
} from 'react-router';

import { getBasket } from '../api/basketApi';
import { createOrder } from '../api/ordersApi';
import type { Basket } from '../types/basket';

export function CheckoutPage() {
    const navigate = useNavigate();

    const [basket, setBasket] = useState<Basket | null>(null);
    const [customerEmail, setCustomerEmail] =
        useState('alice.customer@example.com');
    const [paymentMethod, setPaymentMethod] =
        useState('test-success');
    const [isLoading, setIsLoading] = useState(true);
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [errorMessage, setErrorMessage] =
        useState<string | null>(null);

    useEffect(() => {
        async function loadBasket() {
            setIsLoading(true);
            setErrorMessage(null);

            try {
                setBasket(await getBasket());
            } catch (error) {
                setErrorMessage(getErrorMessage(error));
            } finally {
                setIsLoading(false);
            }
        }

        void loadBasket();
    }, []);

    async function handleSubmit(
        event: SubmitEvent<HTMLFormElement>,
    ) {
        event.preventDefault();

        setIsSubmitting(true);
        setErrorMessage(null);

        try {
            const order = await createOrder({
                customerEmail,
                paymentMethod,
            });

            navigate(`/orders/${order.id}`);
        } catch (error) {
            setErrorMessage(getErrorMessage(error));
        } finally {
            setIsSubmitting(false);
        }
    }

    if (isLoading) {
        return (
            <main className="app-shell">
                <section className="state-card">
                    <h1>Loading checkout</h1>
                    <p>Waiting for Basket Service response.</p>
                </section>
            </main>
        );
    }

    if (!basket || basket.items.length === 0) {
        return (
            <main className="app-shell">
                <section className="state-card">
                    <h1>Basket is empty</h1>
                    <p>Add at least one product before checkout.</p>

                    <Link className="primary-button link-button" to="/">
                        Open product catalog
                    </Link>
                </section>
            </main>
        );
    }

    return (
        <main className="app-shell">
            <section className="page-header">
                <div>
                    <p className="eyebrow">Eshop Capstone</p>
                    <h1>Checkout</h1>
                    <p className="description">
                        Checkout creates a durable order in Orders Service.
                    </p>
                </div>

                <Link className="secondary-button link-button" to="/basket">
                    Back to basket
                </Link>
            </section>

            {errorMessage && (
                <section className="state-card error-card">
                    <h2>Checkout failed</h2>
                    <p>{errorMessage}</p>
                </section>
            )}

            <div className="checkout-layout">
                <form className="checkout-form" onSubmit={handleSubmit}>
                    <h2>Customer details</h2>

                    <label className="form-field">
                        <span>Email</span>

                        <input
                            required
                            type="email"
                            maxLength={320}
                            value={customerEmail}
                            onChange={event => {
                                setCustomerEmail(event.target.value);
                            }}
                        />
                    </label>

                    <label className="form-field">
                        <span>Fake payment method</span>

                        <select
                            value={paymentMethod}
                            onChange={event => {
                                setPaymentMethod(event.target.value);
                            }}
                        >
                            <option value="test-success">
                                Test payment – success
                            </option>

                            <option value="test-fail">
                                Test payment – failure
                            </option>
                        </select>
                    </label>

                    <p className="form-note">
                        Payment is not processed yet. The selected method is stored
                        with the order for the future fake Payments Service flow.
                    </p>

                    <button
                        className="primary-button"
                        type="submit"
                        disabled={isSubmitting}
                    >
                        {isSubmitting
                            ? 'Creating order…'
                            : 'Create order'}
                    </button>
                </form>

                <aside className="checkout-summary">
                    <h2>Order summary</h2>

                    {basket.items.map(item => (
                        <div className="checkout-item" key={item.productId}>
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

                    {basket.totals.map(total => (
                        <div className="checkout-total" key={total.currency}>
                            <span>Total</span>
                            <strong>
                                {formatMoney(total.amount, total.currency)}
                            </strong>
                        </div>
                    ))}
                </aside>
            </div>
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
        : 'Unexpected checkout error.';
}
