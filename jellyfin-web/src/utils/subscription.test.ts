import { describe, expect, test } from 'vitest';

import {
    DEFAULT_SUBSCRIPTION_PRICING,
    isExpiredStatus,
    isExpiredSubscriptionUser,
    normalizeSubscriptionPricing,
    shouldRedirectHomeFromSubscription,
    shouldRedirectToSubscription
} from './subscription';

describe('isExpiredStatus', () => {
    test('uses status when provided', () => {
        expect(isExpiredStatus('Expired', undefined)).toBe(true);
        expect(isExpiredStatus('Active', undefined)).toBe(false);
    });

    test('falls back to expiry date when status is missing', () => {
        expect(isExpiredStatus(undefined, '2000-01-01T00:00:00.000Z')).toBe(true);
        expect(isExpiredStatus(undefined, '2999-01-01T00:00:00.000Z')).toBe(false);
    });
});

describe('isExpiredSubscriptionUser', () => {
    test('returns true for expired non-admin users', () => {
        expect(isExpiredSubscriptionUser({
            Status: 'Expired',
            ExpiryDate: null,
            Policy: {
                IsAdministrator: false
            }
        })).toBe(true);
    });

    test('returns false for admins even when expired', () => {
        expect(isExpiredSubscriptionUser({
            Status: 'Expired',
            ExpiryDate: null,
            Policy: {
                IsAdministrator: true
            }
        })).toBe(false);
    });
});

describe('subscription redirect decisions', () => {
    const expiredUser = {
        Status: 'Expired',
        ExpiryDate: null,
        Policy: {
            IsAdministrator: false
        }
    };

    const activeUser = {
        Status: 'Active',
        ExpiryDate: null,
        Policy: {
            IsAdministrator: false
        }
    };

    test('redirects expired non-admin users to subscription', () => {
        expect(shouldRedirectToSubscription(expiredUser, '/home')).toBe(true);
        expect(shouldRedirectToSubscription(expiredUser, '/subscription')).toBe(false);
    });

    test('redirects active users away from subscription page', () => {
        expect(shouldRedirectHomeFromSubscription(activeUser, '/subscription')).toBe(true);
        expect(shouldRedirectHomeFromSubscription(expiredUser, '/subscription')).toBe(false);
    });
});

describe('normalizeSubscriptionPricing', () => {
    test('uses defaults when values are missing or invalid', () => {
        expect(normalizeSubscriptionPricing({
            BasePricePerMonth: 0,
            OneMonthPrice: 0,
            ThreeMonthPrice: -10,
            SixMonthPrice: Number.NaN
        })).toEqual(DEFAULT_SUBSCRIPTION_PRICING);
    });

    test('accepts decimal pricing values', () => {
        expect(normalizeSubscriptionPricing({
            BasePricePerMonth: 120.5,
            OneMonthPrice: 95.75,
            ThreeMonthPrice: 260.25,
            SixMonthPrice: 480.5,
            TwelveMonthPrice: 900.9
        })).toEqual({
            BasePricePerMonth: 120.5,
            OneMonthPrice: 95.75,
            ThreeMonthPrice: 260.25,
            SixMonthPrice: 480.5,
            TwelveMonthPrice: 900.9
        });
    });

    test('prefers plans array when valid', () => {
        expect(normalizeSubscriptionPricing({
            BasePricePerMonth: 100,
            OneMonthPrice: 999,
            ThreeMonthPrice: 999,
            SixMonthPrice: 999,
            TwelveMonthPrice: 999,
            Plans: [
                { Months: 1, Price: 101 },
                { Months: 3, Price: 252 },
                { Months: 6, Price: 455 },
                { Months: 12, Price: 860 }
            ]
        })).toEqual({
            BasePricePerMonth: 100,
            OneMonthPrice: 101,
            ThreeMonthPrice: 252,
            SixMonthPrice: 455,
            TwelveMonthPrice: 860
        });
    });

    test('supports camelCase additive response shape', () => {
        expect(normalizeSubscriptionPricing({
            basePricePerMonth: 125.5,
            plans: [
                { months: 1, price: 100.5 },
                { months: 3, price: 260.25 },
                { months: 6, price: 500.75 },
                { months: 12, price: 920.1 }
            ]
        })).toEqual({
            BasePricePerMonth: 125.5,
            OneMonthPrice: 100.5,
            ThreeMonthPrice: 260.25,
            SixMonthPrice: 500.75,
            TwelveMonthPrice: 920.1
        });
    });
});
