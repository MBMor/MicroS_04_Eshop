import { createContext } from 'react';

import type { AuthenticationSnapshot } from './keycloakClient';
import type { EshopRole } from './roles';

export type AuthContextValue =
    AuthenticationSnapshot & {
        hasRole: (role: EshopRole) => boolean;
        login: () => Promise<void>;
        logout: () => Promise<void>;
    };

export const AuthContext =
    createContext<AuthContextValue | null>(null);
