export type Product = {
    id: string;
    name: string;
    sku: string;
    description: string;
    category: string;
    priceAmount: number;
    currency: string;
    isActive: boolean;
    createdAtUtc: string;
    updatedAtUtc: string | null;
};
