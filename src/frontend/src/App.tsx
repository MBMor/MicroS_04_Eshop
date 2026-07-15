import {
    NavLink,
    Route,
    Routes,
} from 'react-router';

import './App.css';
import { BasketPage } from './pages/BasketPage';
import { CheckoutPage } from './pages/CheckoutPage';
import { OrderDetailsPage } from './pages/OrderDetailsPage';
import { OrdersPage } from './pages/OrdersPage';
import { ProductCatalogPage } from './pages/ProductCatalogPage';

function App() {
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
                </nav>
            </header>

            <Routes>
                <Route path="/" element={<ProductCatalogPage />} />
                <Route path="/basket" element={<BasketPage />} />
                <Route path="/checkout" element={<CheckoutPage />} />
                <Route path="/orders" element={<OrdersPage />} />

                <Route
                    path="/orders/:orderId"
                    element={<OrderDetailsPage />}
                />
            </Routes>
        </>
    );
}

export default App;
