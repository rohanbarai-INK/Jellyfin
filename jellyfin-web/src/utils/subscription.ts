export interface SubscriptionPricing {
    OneMonthPrice: number
    ThreeMonthPrice: number
    SixMonthPrice: number
    TwelveMonthPrice: number
}

export type UserWithSubscriptionState = {
    Status?: string | null
    status?: string | null
    ExpiryDate?: string | null
    expiryDate?: string | null
    Policy?: {
        IsAdministrator?: boolean | null
        isAdministrator?: boolean | null
    } | null
    policy?: {
        IsAdministrator?: boolean | null
        isAdministrator?: boolean | null
    } | null
    localUser?: UserWithSubscriptionState
    LocalUser?: UserWithSubscriptionState
} | null | undefined;

export const SUBSCRIPTION_CONFIG_KEY = 'subscription';
export const SUBSCRIPTION_ROUTE = '/subscription';
export const SUBSCRIPTION_HASH_ROUTE = '#/subscription';

export const DEFAULT_SUBSCRIPTION_PRICING: SubscriptionPricing = {
    OneMonthPrice: 100,
    ThreeMonthPrice: 250,
    SixMonthPrice: 450,
    TwelveMonthPrice: 850
};

const parsePositiveInteger = (value: unknown, fallback: number) => {
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
};

export const normalizeSubscriptionPricing = (config: Partial<SubscriptionPricing> | undefined | null): SubscriptionPricing => ({
    OneMonthPrice: parsePositiveInteger(config?.OneMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.OneMonthPrice),
    ThreeMonthPrice: parsePositiveInteger(config?.ThreeMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.ThreeMonthPrice),
    SixMonthPrice: parsePositiveInteger(config?.SixMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.SixMonthPrice),
    TwelveMonthPrice: parsePositiveInteger(config?.TwelveMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.TwelveMonthPrice)
});

export const isExpiredStatus = (status: string | null | undefined, expiryDate: string | null | undefined) => {
    if (status) {
        return status.toLowerCase() === 'expired';
    }

    if (!expiryDate) {
        return false;
    }

    const parsedDate = new Date(expiryDate);
    return !Number.isNaN(parsedDate.getTime()) && parsedDate.getTime() < Date.now();
};

export const isExpiredSubscriptionUser = (user: UserWithSubscriptionState) => {
    if (!user) {
        return false;
    }

    const localUser = (user.localUser || user.LocalUser || user) as UserWithSubscriptionState;
    const policy = localUser?.Policy || localUser?.policy;
    const isAdministrator = Boolean(policy?.IsAdministrator ?? policy?.isAdministrator);

    if (isAdministrator) {
        return false;
    }

    const status = localUser?.Status ?? localUser?.status;
    const expiryDate = localUser?.ExpiryDate ?? localUser?.expiryDate;

    return isExpiredStatus(status, expiryDate);
};

export const shouldRedirectToSubscription = (user: UserWithSubscriptionState, pathname: string) => (
    isExpiredSubscriptionUser(user) && pathname !== SUBSCRIPTION_ROUTE
);

export const shouldRedirectHomeFromSubscription = (user: UserWithSubscriptionState, pathname: string) => (
    pathname === SUBSCRIPTION_ROUTE && !isExpiredSubscriptionUser(user)
);
