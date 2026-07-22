function resolveRequiredValue(
    value: string | undefined,
    developmentDefault: string,
    variableName: string,
): string {
    const resolvedValue = value?.trim()
        || (import.meta.env.DEV ? developmentDefault : '');

    if (!resolvedValue) {
        throw new Error(
            `Frontend configuration value ${variableName} is required.`,
        );
    }

    return resolvedValue;
}

const keycloakUrl = resolveRequiredValue(
    import.meta.env.VITE_KEYCLOAK_URL,
    'http://localhost:18080',
    'VITE_KEYCLOAK_URL',
).replace(/\/+$/, '');

export const authConfig = {
    keycloakUrl,
    realm: resolveRequiredValue(
        import.meta.env.VITE_KEYCLOAK_REALM,
        'eshop',
        'VITE_KEYCLOAK_REALM',
    ),
    clientId: resolveRequiredValue(
        import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
        'eshop-frontend',
        'VITE_KEYCLOAK_CLIENT_ID',
    ),
} as const;
