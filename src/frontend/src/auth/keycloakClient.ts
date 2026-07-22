import Keycloak, {
    type KeycloakTokenParsed,
} from 'keycloak-js';

import { authConfig } from './authConfig';

const returnPathStorageKey = 'eshop.auth.return-path';

type EshopTokenParsed = KeycloakTokenParsed & {
    preferred_username?: string;
    email?: string;
    roles?: unknown;
};

export type AuthenticationSnapshot = {
    isAuthenticated: boolean;
    subject: string | null;
    username: string | null;
    email: string | null;
    roles: readonly string[];
};

export const keycloak = new Keycloak({
    url: authConfig.keycloakUrl,
    realm: authConfig.realm,
    clientId: authConfig.clientId,
});

let initializationPromise: Promise<boolean> | null = null;
let tokenRefreshPromise: Promise<boolean> | null = null;

export function initializeAuthentication():
    Promise<boolean> {
    initializationPromise ??= keycloak.init({
        onLoad: 'check-sso',
        pkceMethod: 'S256',
        silentCheckSsoRedirectUri:
            `${window.location.origin}/silent-check-sso.html`,
        checkLoginIframe: true,
    });

    return initializationPromise;
}

export function readAuthenticationSnapshot():
    AuthenticationSnapshot {
    const token = keycloak.tokenParsed as
        | EshopTokenParsed
        | undefined;

    return {
        isAuthenticated: keycloak.authenticated === true,
        subject: token?.sub ?? null,
        username: token?.preferred_username ?? null,
        email: token?.email ?? null,
        roles: readRoles(token?.roles),
    };
}

export async function beginLogin(): Promise<void> {
    rememberCurrentPath();

    await keycloak.login({
        redirectUri: getApplicationRootUrl(),
    });
}

export async function beginLogout(): Promise<void> {
    sessionStorage.removeItem(returnPathStorageKey);

    await keycloak.logout({
        redirectUri: getApplicationRootUrl(),
    });
}

export async function getValidAccessToken(
    minimumValiditySeconds = 30,
): Promise<string | null> {
    if (!keycloak.authenticated) {
        return null;
    }

    try {
        await refreshAccessToken(minimumValiditySeconds);
    } catch {
        keycloak.clearToken();

        throw new Error(
            'The authentication session expired. Sign in again.',
        );
    }

    const accessToken = keycloak.token;

    if (!accessToken) {
        keycloak.clearToken();

        throw new Error(
            'The authentication session does not contain an access token.',
        );
    }

    return accessToken;
}

export async function refreshAccessToken(
    minimumValiditySeconds = 30,
): Promise<boolean> {
    if (!keycloak.authenticated) {
        return false;
    }

    tokenRefreshPromise ??= keycloak
        .updateToken(minimumValiditySeconds)
        .finally(() => {
            tokenRefreshPromise = null;
        });

    return await tokenRefreshPromise;
}

export function restorePostLoginPath(): void {
    const returnPath = sessionStorage.getItem(
        returnPathStorageKey,
    );

    sessionStorage.removeItem(returnPathStorageKey);

    if (
        !keycloak.authenticated
        || !returnPath
        || !isSafeRelativePath(returnPath)
    ) {
        return;
    }

    window.history.replaceState(
        window.history.state,
        '',
        returnPath,
    );
}

function readRoles(
    value: unknown,
): readonly string[] {
    if (!Array.isArray(value)) {
        return [];
    }

    const roles = value.filter(
        (role): role is string =>
            typeof role === 'string'
            && role.trim().length > 0,
    );

    return [
        ...new Set(roles),
    ].sort((left, right) =>
        left.localeCompare(right));
}

function rememberCurrentPath(): void {
    const returnPath =
        `${window.location.pathname}`
        + `${window.location.search}`
        + `${window.location.hash}`;

    if (isSafeRelativePath(returnPath)) {
        sessionStorage.setItem(
            returnPathStorageKey,
            returnPath,
        );
    }
}

function isSafeRelativePath(
    path: string,
): boolean {
    return path.startsWith('/')
        && !path.startsWith('//');
}

function getApplicationRootUrl(): string {
    return `${window.location.origin}/`;
}
