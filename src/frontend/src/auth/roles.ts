export const eshopRoles = {
    customer: 'customer',
    support: 'support',
    admin: 'admin',
} as const;

export type EshopRole =
    (typeof eshopRoles)[keyof typeof eshopRoles];
