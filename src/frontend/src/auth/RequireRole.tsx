import type { ReactNode } from 'react';

import { useAuth } from './useAuth';
import type { EshopRole } from './roles';

type RequireRoleProps = {
    role: EshopRole;
    children: ReactNode;
};

export function RequireRole({
    role,
    children,
}: RequireRoleProps) {
    const {
        isAuthenticated,
        hasRole,
        login,
    } = useAuth();

    if (!isAuthenticated) {
        return (
            <main className="app-shell">
                <section className="state-card">
                    <h1>Sign in required</h1>

                    <p>
                        Sign in to access this part of the Eshop.
                    </p>

                    <button
                        className="primary-button"
                        type="button"
                        onClick={() => {
                            void login();
                        }}
                    >
                        Sign in
                    </button>
                </section>
            </main>
        );
    }

    if (!hasRole(role)) {
        return (
            <main className="app-shell">
                <section className="state-card error-card">
                    <h1>Access denied</h1>

                    <p>
                        Your account does not have the required
                        application role.
                    </p>
                </section>
            </main>
        );
    }

    return children;
}
