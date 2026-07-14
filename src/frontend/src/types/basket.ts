export type BasketItem = {
    productId: string;
    productName: string;
    unitPrice: number;
    currency: string;
    quantity: number;
    lineTotal: number;
};

export type BasketTotal = {
    currency: string;
    amount: number;
};

export type Basket = {
    items: BasketItem[];
    totals: BasketTotal[];
    updatedAtUtc: string;
    expiresAtUtc: string;
};
