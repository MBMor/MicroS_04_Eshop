import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';

import App from './App';
import { AuthProvider } from './auth/AuthProvider';
import {
    initializeAuthentication,
    restorePostLoginPath,
} from './auth/keycloakClient';
import './index.css';

const rootElement = document.getElementById('root');

if (!rootElement) {
    throw new Error(
        'Root element with id "root" was not found.',
    );
}

const root = createRoot(rootElement);

async function bootstrapApplication(): Promise<void> {
    try {
        await initializeAuthentication();
        restorePostLoginPath();

        root.render(
            <StrictMode>
                <AuthProvider>
                    <BrowserRouter>
                        <App />
                    </BrowserRouter>
                </AuthProvider>
            </StrictMode>,
        );
    } catch (error) {
        const message = error instanceof Error
            ? error.message
            : 'Unexpected authentication initialization error.';

        root.render(
            <StrictMode>
                <main className="app-shell">
                    <section className="state-card error-card">
                        <h1>Application initialization failed</h1>
                        <p>{message}</p>
                    </section>
                </main>
            </StrictMode>,
        );
    }
}

void bootstrapApplication();
