const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim();

const normalizedBaseUrl = configuredBaseUrl
    ? configuredBaseUrl.replace(/\/+$/, '')
    : '';

export const apiConfig = {
    baseUrl: normalizedBaseUrl,
    developmentCustomerId:
        import.meta.env.VITE_DEVELOPMENT_CUSTOMER_ID?.trim() ?? '',
} as const;

export function buildApiUrl(path: string): string {
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;

    return `${apiConfig.baseUrl}${normalizedPath}`;
}
