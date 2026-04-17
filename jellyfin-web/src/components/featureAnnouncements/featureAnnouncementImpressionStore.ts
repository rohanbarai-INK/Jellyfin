import type { FeatureAnnouncementCampaign } from './featureAnnouncementTypes';

interface CampaignImpressionRecord {
    impressions: number
    firstShownAt: string
    lastShownAt: string
    dailyImpressions?: Record<string, number>
}

type CampaignImpressionMap = Record<string, CampaignImpressionRecord>;

const STORAGE_KEY_PREFIX = 'jellyfin.featureAnnouncements.v1';

const getStorageKey = (userId: string, serverId: string) => `${STORAGE_KEY_PREFIX}:${serverId}:${userId}`;

const safeGetLocalStorageItem = (key: string) => {
    try {
        return window.localStorage.getItem(key);
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to read localStorage key', error);
        return null;
    }
};

const safeSetLocalStorageItem = (key: string, value: string) => {
    try {
        window.localStorage.setItem(key, value);
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to write localStorage key', error);
    }
};

const parseImpressionMap = (value: string | null): CampaignImpressionMap => {
    if (!value) {
        return {};
    }

    try {
        const parsed = JSON.parse(value) as CampaignImpressionMap;
        if (!parsed || typeof parsed !== 'object') {
            return {};
        }

        return parsed;
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to parse impression map', error);
        return {};
    }
};

const loadImpressionMap = (userId: string, serverId: string): CampaignImpressionMap => {
    const storageKey = getStorageKey(userId, serverId);
    return parseImpressionMap(safeGetLocalStorageItem(storageKey));
};

const saveImpressionMap = (userId: string, serverId: string, map: CampaignImpressionMap) => {
    const storageKey = getStorageKey(userId, serverId);
    safeSetLocalStorageItem(storageKey, JSON.stringify(map));
};

const toUtcDayKey = (isoValue: string) => {
    const date = new Date(isoValue);
    if (Number.isNaN(date.getTime())) {
        const fallback = new Date();
        return fallback.toISOString().slice(0, 10);
    }

    return date.toISOString().slice(0, 10);
};

export const getCampaignImpressionCount = (
    userId: string,
    serverId: string,
    campaignId: string
) => {
    const map = loadImpressionMap(userId, serverId);
    return map[campaignId]?.impressions || 0;
};

export const getCampaignDailyImpressionCount = (
    userId: string,
    serverId: string,
    campaignId: string,
    dayKey: string
) => {
    const map = loadImpressionMap(userId, serverId);
    return map[campaignId]?.dailyImpressions?.[dayKey] || 0;
};

export const recordCampaignImpression = (
    userId: string,
    serverId: string,
    campaignId: string,
    shownAtIso: string
) => {
    const map = loadImpressionMap(userId, serverId);
    const existing = map[campaignId];
    const dayKey = toUtcDayKey(shownAtIso);
    const existingDaily = existing?.dailyImpressions || {};

    map[campaignId] = {
        impressions: (existing?.impressions || 0) + 1,
        firstShownAt: existing?.firstShownAt || shownAtIso,
        lastShownAt: shownAtIso,
        dailyImpressions: {
            ...existingDaily,
            [dayKey]: (existingDaily[dayKey] || 0) + 1
        }
    };

    saveImpressionMap(userId, serverId, map);
};

const isCampaignWithinWindow = (campaign: FeatureAnnouncementCampaign, now: Date) => {
    if (campaign.startsAt) {
        const startsAt = new Date(campaign.startsAt);
        if (!Number.isNaN(startsAt.getTime()) && now < startsAt) {
            return false;
        }
    }

    if (campaign.endsAt) {
        const endsAt = new Date(campaign.endsAt);
        if (!Number.isNaN(endsAt.getTime()) && now > endsAt) {
            return false;
        }
    }

    return true;
};

interface NextCampaignParams {
    campaigns: FeatureAnnouncementCampaign[]
    userId: string
    serverId: string
    now: Date
    sessionShownCampaignIds: Set<string>
}

export const getEligibleCampaigns = ({
    campaigns,
    userId,
    serverId,
    now,
    sessionShownCampaignIds
}: NextCampaignParams): FeatureAnnouncementCampaign[] => {
    const nowDayKey = now.toISOString().slice(0, 10);
    const sortedCampaigns = [ ...campaigns ]
        .sort((left, right) => {
            const priorityDiff = (right.priority || 0) - (left.priority || 0);
            if (priorityDiff !== 0) {
                return priorityDiff;
            }

            return (right.sortOrder || 0) - (left.sortOrder || 0);
        });

    const eligibleCampaigns: FeatureAnnouncementCampaign[] = [];

    for (const campaign of sortedCampaigns) {
        if (!campaign.enabled || campaign.maxImpressionsPerUser <= 0) {
            continue;
        }

        if (!isCampaignWithinWindow(campaign, now)) {
            continue;
        }

        if (sessionShownCampaignIds.has(campaign.id)) {
            continue;
        }

        const impressions = getCampaignImpressionCount(userId, serverId, campaign.id);
        if (impressions >= campaign.maxImpressionsPerUser) {
            continue;
        }

        const dailyLimit = campaign.maxImpressionsPerDay || 0;
        if (dailyLimit > 0) {
            const dailyImpressions = getCampaignDailyImpressionCount(userId, serverId, campaign.id, nowDayKey);
            if (dailyImpressions >= dailyLimit) {
                continue;
            }
        }

        eligibleCampaigns.push(campaign);
    }

    return eligibleCampaigns;
};

export const getNextEligibleCampaign = (params: NextCampaignParams): FeatureAnnouncementCampaign | null => {
    const campaigns = getEligibleCampaigns(params);
    return campaigns[0] || null;
};
