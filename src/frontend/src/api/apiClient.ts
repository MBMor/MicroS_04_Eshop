import { getValidAccessToken } from '../auth/keycloakClient';
import { buildApiUrl } from '../config/apiConfig';

type ProblemDetails = {
    title?: string;
    detail?: string;
};

export class ApiError extends Error {
    public readonly status: number;

    public constructor(
        message: string,
        status: number,
    ) {
        super(message);
        this.name = 'ApiError';
        this.status = status;
    }
}

export async function apiRequest<T>(
    path: string,
    requestInit: RequestInit = {},
): Promise<T> {
    const headers = new Headers(requestInit.headers);

    headers.set('Accept', 'application/json');

    const accessToken = await getValidAccessToken();

    if (accessToken) {
        headers.set(
            'Authorization',
            `Bearer ${accessToken}`,
        );
    }

    const response = await fetch(
        buildApiUrl(path),
        {
            ...requestInit,
            headers,
        },
    );

    if (!response.ok) {
        throw new ApiError(
            await readErrorMessage(response),
            response.status,
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return await response.json() as T;
}

async function readErrorMessage(
    response: Response,
): Promise<string> {
    const fallbackMessage =
        getStatusFallbackMessage(response.status);

    try {
        const problem =
            await response.json() as ProblemDetails;

        return problem.detail
            ?? problem.title
            ?? fallbackMessage;
    } catch {
        return fallbackMessage;
    }
}

function getStatusFallbackMessage(
    status: number,
): string {
    if (status === 401) {
        return 'Authentication is required or the session expired.';
    }

    if (status === 403) {
        return 'You do not have permission to perform this operation.';
    }

    return `API request failed with status ${status}.`;
}
