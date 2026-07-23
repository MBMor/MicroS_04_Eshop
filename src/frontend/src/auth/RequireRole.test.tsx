import {
    cleanup,
    fireEvent,
    render,
    screen,
} from '@testing-library/react';
import {
    afterEach,
    describe,
    expect,
    it,
    vi,
} from 'vitest';

import { RequireRole } from './RequireRole';
import type { EshopRole } from './roles';
import { useAuth } from './useAuth';

vi.mock('./useAuth', () => ({
    useAuth: vi.fn(),
}));

const mockedUseAuth = vi.mocked(useAuth);

const requiredRole: EshopRole = 'admin';

function createAuthMock(
    overrides: Partial<ReturnType<typeof useAuth>> = {},
): ReturnType<typeof useAuth> {
    return {
        isAuthenticated: false,
        subject: 'test-subject',
        username: 'test-user',
        email: 'test@example.com',
        roles: [],
        hasRole: vi
            .fn<(role: EshopRole) => boolean>()
            .mockReturnValue(false),
        login: vi
            .fn<() => Promise<void>>()
            .mockResolvedValue(undefined),
        logout: vi
            .fn<() => Promise<void>>()
            .mockResolvedValue(undefined),
        ...overrides,
    };
}

afterEach(() => {
    cleanup();
    vi.resetAllMocks();
});

describe('RequireRole', () => {
    it('shows the sign-in state when the user is not authenticated', () => {
        const auth = createAuthMock({
            isAuthenticated: false,
        });

        mockedUseAuth.mockReturnValue(auth);

        render(
            <RequireRole role={requiredRole}>
                <div>Protected content</div>
            </RequireRole>,
        );

        expect(
            screen.getByRole('heading', {
                name: 'Sign in required',
            }),
        ).toBeInTheDocument();

        expect(
            screen.getByText(
                'Sign in to access this part of the Eshop.',
            ),
        ).toBeInTheDocument();

        expect(
            screen.queryByText('Protected content'),
        ).not.toBeInTheDocument();

        expect(auth.hasRole).not.toHaveBeenCalled();
    });

    it('calls login when the sign-in button is clicked', () => {
        const auth = createAuthMock({
            isAuthenticated: false,
        });

        mockedUseAuth.mockReturnValue(auth);

        render(
            <RequireRole role={requiredRole}>
                <div>Protected content</div>
            </RequireRole>,
        );

        fireEvent.click(
            screen.getByRole('button', {
                name: 'Sign in',
            }),
        );

        expect(auth.login).toHaveBeenCalledOnce();
    });

    it('shows access denied when the user does not have the required role', () => {
        const hasRole = vi
            .fn<(role: EshopRole) => boolean>()
            .mockReturnValue(false);

        const auth = createAuthMock({
            isAuthenticated: true,
            roles: [],
            hasRole,
        });

        mockedUseAuth.mockReturnValue(auth);

        render(
            <RequireRole role={requiredRole}>
                <div>Protected content</div>
            </RequireRole>,
        );

        expect(
            screen.getByRole('heading', {
                name: 'Access denied',
            }),
        ).toBeInTheDocument();

        expect(
            screen.getByText(
                'Your account does not have the required application role.',
            ),
        ).toBeInTheDocument();

        expect(
            screen.queryByText('Protected content'),
        ).not.toBeInTheDocument();

        expect(hasRole).toHaveBeenCalledOnce();
        expect(hasRole).toHaveBeenCalledWith(requiredRole);
    });

    it('renders children when the user has the required role', () => {
        const hasRole = vi
            .fn<(role: EshopRole) => boolean>()
            .mockReturnValue(true);

        const auth = createAuthMock({
            isAuthenticated: true,
            roles: [requiredRole],
            hasRole,
        });

        mockedUseAuth.mockReturnValue(auth);

        render(
            <RequireRole role={requiredRole}>
                <div>Protected content</div>
            </RequireRole>,
        );

        expect(
            screen.getByText('Protected content'),
        ).toBeInTheDocument();

        expect(
            screen.queryByRole('heading', {
                name: 'Sign in required',
            }),
        ).not.toBeInTheDocument();

        expect(
            screen.queryByRole('heading', {
                name: 'Access denied',
            }),
        ).not.toBeInTheDocument();

        expect(hasRole).toHaveBeenCalledOnce();
        expect(hasRole).toHaveBeenCalledWith(requiredRole);
    });
});
