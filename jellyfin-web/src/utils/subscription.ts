export interface SubscriptionPricing {
    BasePricePerMonth: number
    OneMonthPrice: number
    ThreeMonthPrice: number
    SixMonthPrice: number
    TwelveMonthPrice: number
}

type SubscriptionPlanEntry = {
    Months?: unknown
    months?: unknown
    Price?: unknown
    price?: unknown
};

export type SubscriptionPricingConfig = Partial<SubscriptionPricing> & {
    basePricePerMonth?: unknown
    Plans?: SubscriptionPlanEntry[] | null
    plans?: SubscriptionPlanEntry[] | null
};

type UserPolicy = {
    IsAdministrator?: boolean | null
    isAdministrator?: boolean | null
} | null | undefined;

type UserWithSubscriptionStateShape = {
    Status?: string | null
    status?: string | null
    ExpiryDate?: string | null
    expiryDate?: string | null
    Policy?: UserPolicy
    policy?: UserPolicy
    localUser?: UserWithSubscriptionStateShape
    LocalUser?: UserWithSubscriptionStateShape
};

export type UserWithSubscriptionState = UserWithSubscriptionStateShape | null | undefined;

export const SUBSCRIPTION_CONFIG_KEY = 'subscription';
export const SUBSCRIPTION_ROUTE = '/subscription';
export const SUBSCRIPTION_HASH_ROUTE = '#/subscription';

export const DEFAULT_SUBSCRIPTION_PRICING: SubscriptionPricing = {
    BasePricePerMonth: 100,
    OneMonthPrice: 100,
    ThreeMonthPrice: 250,
    SixMonthPrice: 450,
    TwelveMonthPrice: 850
};

const PLAN_MONTHS = [ 1, 3, 6, 12 ] as const;

const parsePositiveNumber = (value: unknown, fallback: number) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
};

const isPlanDuration = (value: number): value is typeof PLAN_MONTHS[number] => (
    PLAN_MONTHS.some(months => months === value)
);

const getPlanMap = (config: SubscriptionPricingConfig | undefined | null) => {
    const configuredPlans = config?.Plans || config?.plans || [];
    const planMap = new Map<typeof PLAN_MONTHS[number], number>();

    for (const configuredPlan of configuredPlans) {
        if (!configuredPlan) {
            continue;
        }

        const months = Number(configuredPlan.Months ?? configuredPlan.months);
        if (!Number.isInteger(months) || !isPlanDuration(months)) {
            continue;
        }

        const price = parsePositiveNumber(
            configuredPlan.Price ?? configuredPlan.price,
            Number.NaN);
        if (!Number.isFinite(price) || price <= 0) {
            continue;
        }

        planMap.set(months, price);
    }

    return planMap;
};

export const normalizeSubscriptionPricing = (config: SubscriptionPricingConfig | undefined | null): SubscriptionPricing => {
    const planMap = getPlanMap(config);

    return {
        BasePricePerMonth: parsePositiveNumber(
            config?.BasePricePerMonth ?? config?.basePricePerMonth,
            DEFAULT_SUBSCRIPTION_PRICING.BasePricePerMonth),
        OneMonthPrice: planMap.get(1)
            ?? parsePositiveNumber(config?.OneMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.OneMonthPrice),
        ThreeMonthPrice: planMap.get(3)
            ?? parsePositiveNumber(config?.ThreeMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.ThreeMonthPrice),
        SixMonthPrice: planMap.get(6)
            ?? parsePositiveNumber(config?.SixMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.SixMonthPrice),
        TwelveMonthPrice: planMap.get(12)
            ?? parsePositiveNumber(config?.TwelveMonthPrice, DEFAULT_SUBSCRIPTION_PRICING.TwelveMonthPrice)
    };
};

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
