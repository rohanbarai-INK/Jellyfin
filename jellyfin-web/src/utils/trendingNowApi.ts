import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';

export type TrendingNowPeriod = 'week' | 'month' | 'season';
export type TrendingExplanationSource = 'BaseTrending' | 'Personalization' | 'AdminPromotion';
export type TrendingAudienceSegment =
    | 'AllUsers'
    | 'NewOrLowHistory'
    | 'ReturningUsers'
    | 'MovieHeavy'
    | 'SeriesHeavy'
    | 'TopGenreMatch';

export interface TrendingNowRailItem {
    itemId: string;
    itemType: string;
    title: string;
    rank: number;
    baseScore: number;
    personalizationBoost: number;
    adminBoost: number;
    finalScore: number;
    totalWatchHours: number;
    uniqueViewers: number;
    starts: number;
    completions: number;
    momentumWatchHours: number;
    promotionId: string | null;
    pinPosition: number | null;
    isAdminPromoted: boolean;
    primaryLabel: string;
    secondaryLabel: string;
    explanationText: string;
    explanationSource: TrendingExplanationSource;
    tagline: string;
    matchedGenre: string;
    audienceSegment: TrendingAudienceSegment | null;
    overview: string;
    genres: string[];
    productionYear: number | null;
    runTimeTicks: number | null;
    officialRating: string;
    hasPrimaryImage: boolean;
    hasBackdropImage: boolean;
    contextText: string;
}

export interface TrendingNowResponse {
    periodKey: string;
    periodLabel: string;
    periodStartUtc: string;
    periodEndUtc: string;
    limit: number;
    candidateCount: number;
    usedFallbackMode: boolean;
    items: TrendingNowRailItem[];
}

export interface TrendingNowSettings {
    maxSlides: number;
}

type UnknownRecord = Record<string, unknown>;

const getApiClient = (apiClient?: ApiClient) => {
    const resolved = apiClient || ServerConnections.currentApiClient();
    if (!resolved) {
        throw new Error('No API client available.');
    }

    return resolved;
};

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

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const toExplanationSource = (value: unknown): TrendingExplanationSource => {
    if (typeof value === 'number') {
        return value === 2 ? 'AdminPromotion' : value === 1 ? 'Personalization' : 'BaseTrending';
    }

    const normalized = toStringValue(value).trim().toLowerCase();
    if (normalized === 'adminpromotion') {
        return 'AdminPromotion';
    }

    if (normalized === 'personalization') {
        return 'Personalization';
    }

    return 'BaseTrending';
};

const toAudienceSegment = (value: unknown): TrendingAudienceSegment | null => {
    if (typeof value === 'number') {
        return [ 'AllUsers', 'NewOrLowHistory', 'ReturningUsers', 'MovieHeavy', 'SeriesHeavy', 'TopGenreMatch' ][value] as TrendingAudienceSegment || null;
    }

    const normalized = toStringValue(value).trim();
    return normalized ? normalized as TrendingAudienceSegment : null;
};

const toTrendingItem = (source: unknown): TrendingNowRailItem => ({
    itemId: toStringValue(read(source, 'ItemId', 'itemId')),
    itemType: toStringValue(read(source, 'ItemType', 'itemType')),
    title: toStringValue(read(source, 'Title', 'title')),
    rank: toNumberValue(read(source, 'Rank', 'rank')),
    baseScore: toNumberValue(read(source, 'BaseScore', 'baseScore')),
    personalizationBoost: toNumberValue(read(source, 'PersonalizationBoost', 'personalizationBoost')),
    adminBoost: toNumberValue(read(source, 'AdminBoost', 'adminBoost')),
    finalScore: toNumberValue(read(source, 'FinalScore', 'finalScore')),
    totalWatchHours: toNumberValue(read(source, 'TotalWatchHours', 'totalWatchHours')),
    uniqueViewers: toNumberValue(read(source, 'UniqueViewers', 'uniqueViewers')),
    starts: toNumberValue(read(source, 'Starts', 'starts')),
    completions: toNumberValue(read(source, 'Completions', 'completions')),
    momentumWatchHours: toNumberValue(read(source, 'MomentumWatchHours', 'momentumWatchHours')),
    promotionId: toNullableString(read(source, 'PromotionId', 'promotionId')),
    pinPosition: toNullableNumber(read(source, 'PinPosition', 'pinPosition')),
    isAdminPromoted: toBooleanValue(read(source, 'IsAdminPromoted', 'isAdminPromoted')),
    primaryLabel: toStringValue(read(source, 'PrimaryLabel', 'primaryLabel')),
    secondaryLabel: toStringValue(read(source, 'SecondaryLabel', 'secondaryLabel')),
    explanationText: toStringValue(read(source, 'ExplanationText', 'explanationText')),
    explanationSource: toExplanationSource(read(source, 'ExplanationSource', 'explanationSource')),
    tagline: toStringValue(read(source, 'Tagline', 'tagline')),
    matchedGenre: toStringValue(read(source, 'MatchedGenre', 'matchedGenre')),
    audienceSegment: toAudienceSegment(read(source, 'AudienceSegment', 'audienceSegment')),
    overview: toStringValue(read(source, 'Overview', 'overview')),
    genres: toArray(read(source, 'Genres', 'genres'))
        .map(item => toStringValue(item).trim())
        .filter(Boolean),
    productionYear: toNullableNumber(read(source, 'ProductionYear', 'productionYear')),
    runTimeTicks: toNullableNumber(read(source, 'RunTimeTicks', 'runTimeTicks')),
    officialRating: toStringValue(read(source, 'OfficialRating', 'officialRating')),
    hasPrimaryImage: toBooleanValue(read(source, 'HasPrimaryImage', 'hasPrimaryImage')),
    hasBackdropImage: toBooleanValue(read(source, 'HasBackdropImage', 'hasBackdropImage')),
    contextText: toStringValue(read(source, 'ContextText', 'contextText'))
});

const toTrendingResponse = (source: unknown): TrendingNowResponse => ({
    periodKey: toStringValue(read(source, 'PeriodKey', 'periodKey')),
    periodLabel: toStringValue(read(source, 'PeriodLabel', 'periodLabel')),
    periodStartUtc: toStringValue(read(source, 'PeriodStartUtc', 'periodStartUtc')),
    periodEndUtc: toStringValue(read(source, 'PeriodEndUtc', 'periodEndUtc')),
    limit: toNumberValue(read(source, 'Limit', 'limit')),
    candidateCount: toNumberValue(read(source, 'CandidateCount', 'candidateCount')),
    usedFallbackMode: toBooleanValue(read(source, 'UsedFallbackMode', 'usedFallbackMode')),
    items: toArray(read(source, 'Items', 'items')).map(toTrendingItem)
});

const toTrendingSettings = (source: unknown): TrendingNowSettings => ({
    maxSlides: toNumberValue(read(source, 'MaxSlides', 'maxSlides'), 12)
});

export const getTrendingNow = async (
    period: TrendingNowPeriod,
    limit = 16,
    apiClient?: ApiClient
): Promise<TrendingNowResponse> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('api/activity/trending-now', {
            period,
            limit
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toTrendingResponse(response);
};

export const getTrendingNowSettings = async (apiClient?: ApiClient): Promise<TrendingNowSettings> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('api/activity/trending-now/settings'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toTrendingSettings(response);
};

export const setTrendingNowSettings = async (
    maxSlides: number,
    apiClient?: ApiClient
): Promise<TrendingNowSettings> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('api/activity/trending-now/settings'),
        data: JSON.stringify({
            MaxSlides: maxSlides
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toTrendingSettings(response);
};
