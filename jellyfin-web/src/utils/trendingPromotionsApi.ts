import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';
import type { TrendingAudienceSegment } from './trendingNowApi';

export interface AdminTrendingPromotion {
    id: string;
    promotionId: string;
    itemId: string;
    itemTitle: string;
    enabled: boolean;
    startsAtUtc: string | null;
    endsAtUtc: string | null;
    pinPosition: number | null;
    boostAmount: number;
    audienceSegment: TrendingAudienceSegment;
    audienceValue: string;
    labelOverride: string;
    taglineOverride: string;
    artworkVariant: string;
    createdAtUtc: string;
    updatedAtUtc: string;
    createdByUsername: string;
    updatedByUsername: string;
}

export interface UpsertTrendingPromotionPayload {
    id?: string;
    promotionId: string;
    itemId: string;
    enabled: boolean;
    startsAtUtc: string | null;
    endsAtUtc: string | null;
    pinPosition: number | null;
    boostAmount: number;
    audienceSegment: TrendingAudienceSegment;
    audienceValue: string;
    labelOverride: string;
    taglineOverride: string;
    artworkVariant: string;
}

type UnknownRecord = Record<string, unknown>;

const read = (source: unknown, ...keys: string[]) => {
    if (!source || typeof source !== 'object') {
        return undefined;
    }

    const record = source as UnknownRecord;
    for (const key of keys) {
        if (Object.prototype.hasOwnProperty.call(record, key)) {
            return record[key];
        }
    }

    return undefined;
};

const toStringValue = (value: unknown, fallback = '') => (
    typeof value === 'string' ? value : fallback
);

const toNullableString = (value: unknown): string | null => {
    const normalized = toStringValue(value).trim();
    return normalized ? normalized : null;
};

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toNumberValue = (value: unknown, fallback = 0) => {
    const parsed = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

const toNullableNumber = (value: unknown): number | null => {
    if (value === null || value === undefined || value === '') {
        return null;
    }

    const parsed = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(parsed) ? parsed : null;
};

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const getApiClient = (apiClient?: ApiClient) => {
    const resolved = apiClient || ServerConnections.currentApiClient();
    if (!resolved) {
        throw new Error('No API client available.');
    }

    return resolved;
};

const toPromotion = (source: unknown): AdminTrendingPromotion => ({
    id: toStringValue(read(source, 'Id', 'id')),
    promotionId: toStringValue(read(source, 'PromotionId', 'promotionId')),
    itemId: toStringValue(read(source, 'ItemId', 'itemId')),
    itemTitle: toStringValue(read(source, 'ItemTitle', 'itemTitle')),
    enabled: toBooleanValue(read(source, 'Enabled', 'enabled'), true),
    startsAtUtc: toNullableString(read(source, 'StartsAtUtc', 'startsAtUtc')),
    endsAtUtc: toNullableString(read(source, 'EndsAtUtc', 'endsAtUtc')),
    pinPosition: toNullableNumber(read(source, 'PinPosition', 'pinPosition')),
    boostAmount: toNumberValue(read(source, 'BoostAmount', 'boostAmount')),
    audienceSegment: toStringValue(read(source, 'AudienceSegment', 'audienceSegment'), 'AllUsers') as TrendingAudienceSegment,
    audienceValue: toStringValue(read(source, 'AudienceValue', 'audienceValue')),
    labelOverride: toStringValue(read(source, 'LabelOverride', 'labelOverride')),
    taglineOverride: toStringValue(read(source, 'TaglineOverride', 'taglineOverride')),
    artworkVariant: toStringValue(read(source, 'ArtworkVariant', 'artworkVariant')),
    createdAtUtc: toStringValue(read(source, 'CreatedAtUtc', 'createdAtUtc')),
    updatedAtUtc: toStringValue(read(source, 'UpdatedAtUtc', 'updatedAtUtc')),
    createdByUsername: toStringValue(read(source, 'CreatedByUsername', 'createdByUsername')),
    updatedByUsername: toStringValue(read(source, 'UpdatedByUsername', 'updatedByUsername'))
});

export const getAdminTrendingPromotions = async (apiClient?: ApiClient): Promise<AdminTrendingPromotion[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('api/activity/trending-now/promotions'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toPromotion);
};

export const upsertTrendingPromotion = async (
    payload: UpsertTrendingPromotionPayload,
    apiClient?: ApiClient
): Promise<AdminTrendingPromotion> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('api/activity/trending-now/promotions/upsert'),
        data: JSON.stringify({
            Id: payload.id || undefined,
            PromotionId: payload.promotionId,
            ItemId: payload.itemId,
            Enabled: payload.enabled,
            StartsAtUtc: payload.startsAtUtc,
            EndsAtUtc: payload.endsAtUtc,
            PinPosition: payload.pinPosition,
            BoostAmount: payload.boostAmount,
            AudienceSegment: payload.audienceSegment,
            AudienceValue: payload.audienceValue,
            LabelOverride: payload.labelOverride,
            TaglineOverride: payload.taglineOverride,
            ArtworkVariant: payload.artworkVariant
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toPromotion(response);
};

export const setTrendingPromotionEnabled = async (
    promotionId: string,
    enabled: boolean,
    apiClient?: ApiClient
): Promise<AdminTrendingPromotion> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl(`api/activity/trending-now/promotions/${promotionId}/enabled`),
        data: JSON.stringify({
            Enabled: enabled
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toPromotion(response);
};

export const deleteTrendingPromotion = async (promotionId: string, apiClient?: ApiClient): Promise<void> => {
    const client = getApiClient(apiClient);
    await client.ajax({
        type: 'DELETE',
        url: client.getUrl(`api/activity/trending-now/promotions/${promotionId}`),
        contentType: 'application/json'
    });
};
