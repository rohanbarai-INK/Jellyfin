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
            OneMonthPrice: 0,
            ThreeMonthPrice: -10,
            SixMonthPrice: 450.5
        })).toEqual(DEFAULT_SUBSCRIPTION_PRICING);
    });
});
