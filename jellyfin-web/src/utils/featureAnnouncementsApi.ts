import type { ApiClient } from 'jellyfin-apiclient';

import leaderboardAnnouncementPreview from 'assets/branding/leaderboard-announcement-preview.png';
import requestPopupAccentGif from 'assets/branding/request-popup-accent.gif';
import type { FeatureAnnouncementCampaign } from 'components/featureAnnouncements/featureAnnouncementTypes';
import { ServerConnections } from 'lib/jellyfin-apiclient';

export type AdminFeatureAnnouncementStatus = 'Draft' | 'Published';
export type AdminFeatureAnnouncementCtaTargetType = 'InternalRoute' | 'ExternalUrl';

export interface AdminFeatureAnnouncement {
    id: string
    campaignId: string
    enabled: boolean
    status: AdminFeatureAnnouncementStatus
    heading: string
    title: string
    subtitle: string
    description: string
    highlights: string[]
    helpText: string
    heroGifSource: string
    mediaImageSource: string
    mediaImageAlt: string
    mediaImageCaption: string
    ctaLabel: string
    ctaTargetType: AdminFeatureAnnouncementCtaTargetType
    ctaTarget: string
    closeLabel: string
    startsAtUtc: string | null
    endsAtUtc: string | null
    maxImpressionsPerDay: number
    maxImpressionsTotal: number
    priority: number
    sortOrder: number
    createdAtUtc: string
    updatedAtUtc: string
    createdByUserId: string | null
    createdByUsername: string
    updatedByUserId: string | null
    updatedByUsername: string
}

export interface UpsertAdminFeatureAnnouncementPayload {
    id?: string
    campaignId: string
    enabled: boolean
    status: AdminFeatureAnnouncementStatus
    heading: string
    title: string
    subtitle: string
    description: string
    highlights: string[]
    helpText: string
    heroGifSource: string
    mediaImageSource: string
    mediaImageAlt: string
    mediaImageCaption: string
    ctaLabel: string
    ctaTargetType: AdminFeatureAnnouncementCtaTargetType
    ctaTarget: string
    closeLabel: string
    startsAtUtc: string | null
    endsAtUtc: string | null
    maxImpressionsPerDay: number
    maxImpressionsTotal: number
    priority: number
    sortOrder: number
}

type UnknownRecord = Record<string, unknown>;

const BUILTIN_ASSET_MAP: Record<string, string> = {
    'request-popup-accent': requestPopupAccentGif,
    'leaderboard-announcement-preview': leaderboardAnnouncementPreview
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

const toNullableString = (value: unknown) => (
    typeof value === 'string' && value.length > 0 ? value : null
);

const toNumberValue = (value: unknown, fallback = 0) => {
    if (typeof value === 'number' && Number.isFinite(value)) {
        return value;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const resolveAssetSource = (source: string, fallback: string) => {
    if (!source) {
        return fallback;
    }

    const normalized = source.trim();
    if (!normalized) {
        return fallback;
    }

    if (!normalized.startsWith('builtin:')) {
        return normalized;
    }

    const key = normalized.slice('builtin:'.length).trim().toLowerCase();
    return BUILTIN_ASSET_MAP[key] || fallback;
};

const toAdminStatus = (value: unknown): AdminFeatureAnnouncementStatus => {
    const normalized = toStringValue(value).trim().toLowerCase();
    return normalized === 'published' || normalized === '1' ? 'Published' : 'Draft';
};

const toAdminCtaTargetType = (value: unknown): AdminFeatureAnnouncementCtaTargetType => {
    const normalized = toStringValue(value).trim().toLowerCase();
    return normalized === 'externalurl' || normalized === 'external' || normalized === '1' ? 'ExternalUrl' : 'InternalRoute';
};

const toAdminFeatureAnnouncement = (source: unknown): AdminFeatureAnnouncement => ({
    id: toStringValue(read(source, 'Id', 'id')),
    campaignId: toStringValue(read(source, 'CampaignId', 'campaignId')),
    enabled: toBooleanValue(read(source, 'Enabled', 'enabled'), true),
    status: toAdminStatus(read(source, 'Status', 'status')),
    heading: toStringValue(read(source, 'Heading', 'heading')),
    title: toStringValue(read(source, 'Title', 'title')),
    subtitle: toStringValue(read(source, 'Subtitle', 'subtitle')),
    description: toStringValue(read(source, 'Description', 'description')),
    highlights: toArray(read(source, 'Highlights', 'highlights'))
        .map(item => toStringValue(item).trim())
        .filter(Boolean),
    helpText: toStringValue(read(source, 'HelpText', 'helpText')),
    heroGifSource: toStringValue(read(source, 'HeroGifSource', 'heroGifSource')),
    mediaImageSource: toStringValue(read(source, 'MediaImageSource', 'mediaImageSource')),
    mediaImageAlt: toStringValue(read(source, 'MediaImageAlt', 'mediaImageAlt')),
    mediaImageCaption: toStringValue(read(source, 'MediaImageCaption', 'mediaImageCaption')),
    ctaLabel: toStringValue(read(source, 'CtaLabel', 'ctaLabel')),
    ctaTargetType: toAdminCtaTargetType(read(source, 'CtaTargetType', 'ctaTargetType')),
    ctaTarget: toStringValue(read(source, 'CtaTarget', 'ctaTarget')),
    closeLabel: toStringValue(read(source, 'CloseLabel', 'closeLabel')),
    startsAtUtc: toNullableString(read(source, 'StartsAtUtc', 'startsAtUtc')),
    endsAtUtc: toNullableString(read(source, 'EndsAtUtc', 'endsAtUtc')),
    maxImpressionsPerDay: Math.max(1, toNumberValue(read(source, 'MaxImpressionsPerDay', 'maxImpressionsPerDay'), 2)),
    maxImpressionsTotal: Math.max(1, toNumberValue(read(source, 'MaxImpressionsTotal', 'maxImpressionsTotal'), 10)),
    priority: toNumberValue(read(source, 'Priority', 'priority'), 0),
    sortOrder: toNumberValue(read(source, 'SortOrder', 'sortOrder'), 0),
    createdAtUtc: toStringValue(read(source, 'CreatedAtUtc', 'createdAtUtc')),
    updatedAtUtc: toStringValue(read(source, 'UpdatedAtUtc', 'updatedAtUtc')),
    createdByUserId: toNullableString(read(source, 'CreatedByUserId', 'createdByUserId')),
    createdByUsername: toStringValue(read(source, 'CreatedByUsername', 'createdByUsername')),
    updatedByUserId: toNullableString(read(source, 'UpdatedByUserId', 'updatedByUserId')),
    updatedByUsername: toStringValue(read(source, 'UpdatedByUsername', 'updatedByUsername'))
});

const toCampaign = (announcement: AdminFeatureAnnouncement): FeatureAnnouncementCampaign => ({
    id: announcement.campaignId,
    enabled: announcement.enabled,
    heading: announcement.heading || "What's New?",
    title: announcement.title,
    subtitle: announcement.subtitle,
    description: announcement.description,
    highlights: announcement.highlights,
    helpText: announcement.helpText,
    heroGifPath: resolveAssetSource(announcement.heroGifSource, requestPopupAccentGif),
    mediaAssets: [
        {
            src: resolveAssetSource(announcement.mediaImageSource, leaderboardAnnouncementPreview),
            alt: announcement.mediaImageAlt || 'Announcement media',
            kind: 'image',
            caption: announcement.mediaImageCaption
        }
    ],
    ctaLabel: announcement.ctaLabel || 'Check It Out',
    ctaRoute: announcement.ctaTarget || '/achievements',
    ctaTargetType: announcement.ctaTargetType === 'ExternalUrl' ? 'external' : 'internal',
    closeLabel: announcement.closeLabel || 'Close',
    startsAt: announcement.startsAtUtc || undefined,
    endsAt: announcement.endsAtUtc || undefined,
    maxImpressionsPerDay: Math.max(1, announcement.maxImpressionsPerDay),
    maxImpressionsPerUser: Math.max(1, announcement.maxImpressionsTotal),
    priority: announcement.priority,
    sortOrder: announcement.sortOrder
});

const getApiClient = (apiClient?: ApiClient) => {
    const resolved = apiClient || ServerConnections.currentApiClient();
    if (!resolved) {
        throw new Error('No API client available.');
    }

    return resolved;
};

export const getAdminFeatureAnnouncements = async (apiClient?: ApiClient): Promise<AdminFeatureAnnouncement[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Announcement/Admin'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toAdminFeatureAnnouncement);
};

export const upsertAdminFeatureAnnouncement = async (
    payload: UpsertAdminFeatureAnnouncementPayload,
    apiClient?: ApiClient
): Promise<AdminFeatureAnnouncement> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Announcement/Admin/Upsert'),
        data: JSON.stringify({
            Id: payload.id || undefined,
            CampaignId: payload.campaignId,
            Enabled: payload.enabled,
            Status: payload.status,
            Heading: payload.heading,
            Title: payload.title,
            Subtitle: payload.subtitle,
            Description: payload.description,
            Highlights: payload.highlights,
            HelpText: payload.helpText,
            HeroGifSource: payload.heroGifSource,
            MediaImageSource: payload.mediaImageSource,
            MediaImageAlt: payload.mediaImageAlt,
            MediaImageCaption: payload.mediaImageCaption,
            CtaLabel: payload.ctaLabel,
            CtaTargetType: payload.ctaTargetType,
            CtaTarget: payload.ctaTarget,
            CloseLabel: payload.closeLabel,
            StartsAtUtc: payload.startsAtUtc,
            EndsAtUtc: payload.endsAtUtc,
            MaxImpressionsPerDay: payload.maxImpressionsPerDay,
            MaxImpressionsTotal: payload.maxImpressionsTotal,
            Priority: payload.priority,
            SortOrder: payload.sortOrder
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toAdminFeatureAnnouncement(response);
};

export const fetchActiveFeatureAnnouncementCampaigns = async (apiClient?: ApiClient): Promise<FeatureAnnouncementCampaign[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Announcement/Active'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response)
        .map(toAdminFeatureAnnouncement)
        .map(toCampaign)
        .filter(campaign => campaign.enabled);
};

export const toFeatureAnnouncementCampaignForPreview = (announcement: AdminFeatureAnnouncement): FeatureAnnouncementCampaign => (
    toCampaign(announcement)
);
