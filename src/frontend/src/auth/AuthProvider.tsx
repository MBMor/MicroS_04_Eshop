import {
    type ReactNode,
    useCallback,
    useEffect,
    useMemo,
    useState,
} from 'react';

import {
    beginLogin,
    beginLogout,
    keycloak,
    readAuthenticationSnapshot,
    refreshAccessToken,
    type AuthenticationSnapshot,
} from './keycloakClient';
import {
    AuthContext,
    type AuthContextValue,
} from './authContextValue';
import type { EshopRole } from './roles';

type AuthProviderProps = {
    children: ReactNode;
};

export function AuthProvider({
    children,
}: AuthProviderProps) {
    const [snapshot, setSnapshot] =
        useState<AuthenticationSnapshot>(
            readAuthenticationSnapshot,
        );

    const synchronizeAuthentication =
        useCallback(() => {
            setSnapshot(readAuthenticationSnapshot());
        }, []);

    useEffect(() => {
        keycloak.onAuthSuccess =
            synchronizeAuthentication;

        keycloak.onAuthRefreshSuccess =
            synchronizeAuthentication;

        keycloak.onAuthLogout =
            synchronizeAuthentication;

        keycloak.onAuthError = () => {
            keycloak.clearToken();
            synchronizeAuthentication();
        };

        keycloak.onAuthRefreshError = () => {
            keycloak.clearToken();
            synchronizeAuthentication();
        };

        keycloak.onTokenExpired = () => {
            void refreshAccessToken(30)
                .then(() => {
                    synchronizeAuthentication();
                })
                .catch(() => {
                    keycloak.clearToken();
                    synchronizeAuthentication();
                });
        };

        return () => {
            keycloak.onAuthSuccess = undefined;
            keycloak.onAuthRefreshSuccess = undefined;
            keycloak.onAuthLogout = undefined;
            keycloak.onAuthError = undefined;
            keycloak.onAuthRefreshError = undefined;
            keycloak.onTokenExpired = undefined;
        };
    }, [synchronizeAuthentication]);

    const hasRole = useCallback(
        (role: EshopRole) =>
            snapshot.roles.includes(role),
        [snapshot.roles],
    );

    const value = useMemo<AuthContextValue>(
        () => ({
            ...snapshot,
            hasRole,
            login: beginLogin,
            logout: beginLogout,
        }),
        [
            snapshot,
            hasRole,
        ],
    );

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
}
