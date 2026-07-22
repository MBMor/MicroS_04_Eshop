import {
    NavLink,
    Route,
    Routes,
} from 'react-router';

import './App.css';
import { RequireRole } from './auth/RequireRole';
import { useAuth } from './auth/useAuth';
import { eshopRoles } from './auth/roles';
import { BasketPage } from './pages/BasketPage';
import { CheckoutPage } from './pages/CheckoutPage';
import { OrderDetailsPage } from './pages/OrderDetailsPage';
import { OrdersPage } from './pages/OrdersPage';
import { ProductCatalogPage } from './pages/ProductCatalogPage';

function App() {
    const {
        isAuthenticated,
        username,
        roles,
        hasRole,
        login,
        logout,
    } = useAuth();

    const hasCustomerRole =
        hasRole(eshopRoles.customer);

    return (
        <>
            <header className="site-header">
                <NavLink className="brand" to="/">
                    Eshop
                </NavLink>

                <nav aria-label="Main navigation">
                    <NavLink
                        className={({ isActive }) =>
                            isActive
                                ? 'nav-link active-nav-link'
                                : 'nav-link'
                        }
                        end
                        to="/"
                    >
                        Products
                    </NavLink>

                    {hasCustomerRole && (
                        <>
                            <NavLink
                                className={({ isActive }) =>
                                    isActive
                                        ? 'nav-link active-nav-link'
                                        : 'nav-link'
                                }
                                to="/basket"
                            >
                                Basket
                            </NavLink>

                            <NavLink
                                className={({ isActive }) =>
                                    isActive
                                        ? 'nav-link active-nav-link'
                                        : 'nav-link'
                                }
                                to="/orders"
                            >
                                Orders
                            </NavLink>
                        </>
                    )}
                </nav>

                <div className="auth-controls">
                    {isAuthenticated ? (
                        <>
                            <div className="auth-user">
                                <strong>
                                    {username ?? 'Authenticated user'}
                                </strong>

                                {roles.length > 0 && (
                                    <span>
                                        {roles.join(', ')}
                                    </span>
                                )}
                            </div>

                            <button
                                className="secondary-button"
                                type="button"
                                onClick={() => {
                                    void logout();
                                }}
                            >
                                Sign out
                            </button>
                        </>
                    ) : (
                        <button
                            className="primary-button"
                            type="button"
                            onClick={() => {
                                void login();
                            }}
                        >
                            Sign in
                        </button>
                    )}
                </div>
            </header>

            <Routes>
                <Route
                    path="/"
                    element={<ProductCatalogPage />}
                />

                <Route
                    path="/basket"
                    element={(
                        <RequireRole role={eshopRoles.customer}>
                            <BasketPage />
                        </RequireRole>
                    )}
                />

                <Route
                    path="/checkout"
                    element={(
                        <RequireRole role={eshopRoles.customer}>
                            <CheckoutPage />
                        </RequireRole>
                    )}
                />

                <Route
                    path="/orders"
                    element={(
                        <RequireRole role={eshopRoles.customer}>
                            <OrdersPage />
                        </RequireRole>
                    )}
                />

                <Route
                    path="/orders/:orderId"
                    element={(
                        <RequireRole role={eshopRoles.customer}>
                            <OrderDetailsPage />
                        </RequireRole>
                    )}
                />
            </Routes>
        </>
    );
}

export default App;
