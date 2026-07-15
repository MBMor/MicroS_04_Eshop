export type OrderItem = {
    id: string;
    productId: string;
    productName: string;
    unitPrice: number;
    currency: string;
    quantity: number;
    lineTotal: number;
};

export type Order = {
    id: string;
    customerEmail: string;
    status: string;
    totalAmount: number;
    currency: string;
    paymentMethod: string;
    createdAtUtc: string;
    updatedAtUtc: string | null;
    items: OrderItem[];
};

export type OrderSummary = {
    id: string;
    status: string;
    totalAmount: number;
    currency: string;
    itemCount: number;
    createdAtUtc: string;
    updatedAtUtc: string | null;
};

export type CreateOrderRequest = {
    customerEmail: string;
    paymentMethod: string;
};
