import { useEffect, useState } from 'react';

import { getProducts } from '../api/productsApi';
import type { Product } from '../types/product';

export function ProductCatalogPage() {
    const [products, setProducts] = useState<Product[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [errorMessage, setErrorMessage] = useState<string | null>(null);

    async function loadProducts() {
        setIsLoading(true);
        setErrorMessage(null);

        try {
            const loadedProducts = await getProducts();
            setProducts(loadedProducts);
        } catch (error) {
            const message = error instanceof Error
                ? error.message
                : 'Unexpected error while loading products.';

            setErrorMessage(message);
        } finally {
            setIsLoading(false);
        }
    }

    useEffect(() => {
        let isCancelled = false;

        void getProducts()
            .then(loadedProducts => {
                if (!isCancelled) {
                    setProducts(loadedProducts);
                }
            })
            .catch((error: unknown) => {
                if (!isCancelled) {
                    const message = error instanceof Error
                        ? error.message
                        : 'Unexpected error while loading products.';

                    setErrorMessage(message);
                }
            })
            .finally(() => {
                if (!isCancelled) {
                    setIsLoading(false);
                }
            });

        return () => {
            isCancelled = true;
        };
    }, []);

    return (
        <main className="app-shell">
            <section className="catalog-header">
                <p className="eyebrow">Eshop Capstone</p>
                <h1>Product Catalog</h1>
                <p className="description">
                    Products are loaded through the API Gateway from Catalog Service.
                </p>

                <button className="secondary-button" type="button" onClick={() => void loadProducts()}>
                    Refresh products
                </button>
            </section>

            {isLoading && (
                <section className="state-card">
                    <h2>Loading products</h2>
                    <p>Waiting for Catalog Service response.</p>
                </section>
            )}

            {!isLoading && errorMessage && (
                <section className="state-card error-card">
                    <h2>Catalog is unavailable</h2>
                    <p>{errorMessage}</p>
                    <p>
                        Check that PostgreSQL, CatalogService and ApiGateway are running.
                    </p>
                </section>
            )}

            {!isLoading && !errorMessage && products.length === 0 && (
                <section className="state-card">
                    <h2>No products yet</h2>
                    <p>
                        Create products through the Catalog API and refresh this page.
                    </p>
                </section>
            )}

            {!isLoading && !errorMessage && products.length > 0 && (
                <section className="product-grid" aria-label="Products">
                    {products.map(product => (
                        <article className="product-card" key={product.id}>
                            <div className="product-card-header">
                                <div>
                                    <p className="product-category">{product.category}</p>
                                    <h2>{product.name}</h2>
                                </div>

                                <span className="product-sku">{product.sku}</span>
                            </div>

                            {product.description && (
                                <p className="product-description">{product.description}</p>
                            )}

                            <div className="product-footer">
                                <strong>
                                    {product.priceAmount.toLocaleString('cs-CZ', {
                                        style: 'currency',
                                        currency: product.currency,
                                    })}
                                </strong>

                                <span className={product.isActive ? 'status-active' : 'status-inactive'}>
                                    {product.isActive ? 'Active' : 'Inactive'}
                                </span>
                            </div>
                        </article>
                    ))}
                </section>
            )}
        </main>
    );
}
