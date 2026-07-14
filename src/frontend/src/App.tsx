import { NavLink, Route, Routes } from 'react-router-dom';

import './App.css';
import { BasketPage } from './pages/BasketPage';
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
                            isActive ? 'nav-link active-nav-link' : 'nav-link'
                        }
                        end
                        to="/"
                    >
                        Products
                    </NavLink>

                    <NavLink
                        className={({ isActive }) =>
                            isActive ? 'nav-link active-nav-link' : 'nav-link'
                        }
                        to="/basket"
                    >
                        Basket
                    </NavLink>
                </nav>
            </header>

            <Routes>
                <Route path="/" element={<ProductCatalogPage />} />
                <Route path="/basket" element={<BasketPage />} />
            </Routes>
        </>
    );
}

export default App;
