import type { FeatureAnnouncementCampaign } from './featureAnnouncementTypes';

interface CampaignImpressionRecord {
    impressions: number
    firstShownAt: string
    lastShownAt: string
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

export const getCampaignImpressionCount = (
    userId: string,
    serverId: string,
    campaignId: string
) => {
    const map = loadImpressionMap(userId, serverId);
    return map[campaignId]?.impressions || 0;
};

export const recordCampaignImpression = (
    userId: string,
    serverId: string,
    campaignId: string,
    shownAtIso: string
) => {
    const map = loadImpressionMap(userId, serverId);
    const existing = map[campaignId];

    map[campaignId] = {
        impressions: (existing?.impressions || 0) + 1,
        firstShownAt: existing?.firstShownAt || shownAtIso,
        lastShownAt: shownAtIso
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

export const getNextEligibleCampaign = ({
    campaigns,
    userId,
    serverId,
    now,
    sessionShownCampaignIds
}: NextCampaignParams): FeatureAnnouncementCampaign | null => {
    const sortedCampaigns = [ ...campaigns ]
        .sort((left, right) => (right.priority || 0) - (left.priority || 0));

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

        return campaign;
    }

    return null;
};
