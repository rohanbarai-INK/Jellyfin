import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';

export type AchievementRarity = 'common' | 'uncommon' | 'rare' | 'legendary' | 'epic';

export interface AchievementDefinitionRow {
    id: string
    title: string
    description: string
    imageEmoji: string
    rarity: AchievementRarity
    xp: number
    coins: number
    isSeasonal: boolean
}

export interface UserAchievementRow {
    id: string
    title: string
    description: string
    imageEmoji: string
    rarity: AchievementRarity
    xp: number
    coins: number
    unlockedAt: string
}

export interface AchievementUnlockResult {
    unlocked: boolean
    achievement: UserAchievementRow
}

export interface AchievementSyncResult {
    unlockedAchievements: UserAchievementRow[]
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

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toNumberValue = (value: unknown, fallback = 0) => (
    typeof value === 'number' && Number.isFinite(value) ? value : fallback
);

const normalizeRarity = (value: unknown): AchievementRarity => {
    if (value === 'uncommon' || value === 'rare' || value === 'legendary' || value === 'epic') {
        return value;
    }

    return 'common';
};

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const toDefinitionRow = (source: unknown): AchievementDefinitionRow => ({
    id: toStringValue(read(source, 'Id', 'id')),
    title: toStringValue(read(source, 'Title', 'title')),
    description: toStringValue(read(source, 'Description', 'description')),
    imageEmoji: toStringValue(read(source, 'ImageEmoji', 'imageEmoji')),
    rarity: normalizeRarity(read(source, 'Rarity', 'rarity')),
    xp: toNumberValue(read(source, 'Xp', 'xp')),
    coins: toNumberValue(read(source, 'Coins', 'coins')),
    isSeasonal: toBooleanValue(read(source, 'IsSeasonal', 'isSeasonal'))
});

const toUserAchievementRow = (source: unknown): UserAchievementRow => ({
    id: toStringValue(read(source, 'Id', 'id')),
    title: toStringValue(read(source, 'Title', 'title')),
    description: toStringValue(read(source, 'Description', 'description')),
    imageEmoji: toStringValue(read(source, 'ImageEmoji', 'imageEmoji')),
    rarity: normalizeRarity(read(source, 'Rarity', 'rarity')),
    xp: toNumberValue(read(source, 'Xp', 'xp')),
    coins: toNumberValue(read(source, 'Coins', 'coins')),
    unlockedAt: toStringValue(read(source, 'UnlockedAt', 'unlockedAt'))
});

const toUnlockResult = (source: unknown): AchievementUnlockResult => ({
    unlocked: toBooleanValue(read(source, 'Unlocked', 'unlocked')),
    achievement: toUserAchievementRow(read(source, 'Achievement', 'achievement'))
});

const toSyncResult = (source: unknown): AchievementSyncResult => ({
    unlockedAchievements: toArray(read(source, 'UnlockedAchievements', 'unlockedAchievements')).map(toUserAchievementRow)
});

export const getAchievementDefinitions = async (includeSeasonal = false, apiClient?: ApiClient): Promise<AchievementDefinitionRow[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Achievements/Definitions', {
            includeSeasonal
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toDefinitionRow);
};

export const getAchievementHistory = async (userId?: string, take = 200, apiClient?: ApiClient): Promise<UserAchievementRow[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Achievements/History', {
            userId: userId || undefined,
            take: Math.max(1, take)
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toUserAchievementRow);
};

export const unlockAchievement = async (achievementId: string, apiClient?: ApiClient): Promise<AchievementUnlockResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Achievements/Unlock'),
        data: JSON.stringify({
            AchievementId: achievementId
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toUnlockResult(response);
};

export const syncAchievements = async (apiClient?: ApiClient): Promise<AchievementSyncResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Achievements/Sync'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toSyncResult(response);
};
