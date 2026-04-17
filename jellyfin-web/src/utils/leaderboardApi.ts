import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';

export type LeaderboardMetricType = 'xp' | 'watchtime' | 'movies' | 'series' | 'genres' | 'streak' | 'achievements' | 'requests';

export interface LeaderboardPersonalStats {
    seasonYear: number;
    totalXp: number;
    achievementXp: number;
    achievementCount: number;
    level: number;
    rank: number;
    percentile: number;
    totalUsers: number;
    rankTitle: string;
    rankEmoji: string;
    totalWatchMinutes: number;
    moviesCompleted: number;
    seriesCompleted: number;
    uniqueGenresWatched: number;
    currentStreakDays: number;
    bestStreakDays: number;
    achievementsUnlocked: number;
    approvedRequests: number;
    metricValue: number;
    metricType: LeaderboardMetricType;
    metricLabel: string;
    gapToNext: number;
    gapToTop: number;
    currentUserEntry: LeaderboardEntry | null;
    nextTarget: LeaderboardCompetition | null;
    behindUser: LeaderboardCompetition | null;
}

export interface LeaderboardEntry {
    userId: string;
    userName: string;
    primaryImageTag: string;
    rank: number;
    totalXp: number;
    level: number;
    achievementCount: number;
    rankTitle: string;
    rankEmoji: string;
    badgeKind: string;
    isCurrentUser: boolean;
    metricValue: number;
    metricLabel: string;
    secondaryMetricValue: number;
    secondaryMetricLabel: string;
}

export interface LeaderboardCompetition {
    entry: LeaderboardEntry;
    gapValue: number;
    gapLabel: string;
}

export interface LeaderboardTopResult {
    seasonYear: number;
    metricType: LeaderboardMetricType;
    entries: LeaderboardEntry[];
    currentUserPosition: LeaderboardEntry | null;
    totalUsers: number;
    offset: number;
    limit: number;
    hasMore: boolean;
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

const toNumberValue = (value: unknown, fallback = 0) => {
    const parsed = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const toLeaderboardEntry = (source: unknown): LeaderboardEntry => ({
    userId: toStringValue(read(source, 'UserId', 'userId')),
    userName: toStringValue(read(source, 'UserName', 'userName')),
    primaryImageTag: toStringValue(read(source, 'PrimaryImageTag', 'primaryImageTag')),
    rank: toNumberValue(read(source, 'Rank', 'rank')),
    totalXp: toNumberValue(read(source, 'TotalXp', 'totalXp')),
    level: toNumberValue(read(source, 'Level', 'level')),
    achievementCount: toNumberValue(read(source, 'AchievementCount', 'achievementCount')),
    rankTitle: toStringValue(read(source, 'RankTitle', 'rankTitle')),
    rankEmoji: toStringValue(read(source, 'RankEmoji', 'rankEmoji')),
    badgeKind: toStringValue(read(source, 'BadgeKind', 'badgeKind')),
    isCurrentUser: toBooleanValue(read(source, 'IsCurrentUser', 'isCurrentUser')),
    metricValue: toNumberValue(read(source, 'MetricValue', 'metricValue')),
    metricLabel: toStringValue(read(source, 'MetricLabel', 'metricLabel')),
    secondaryMetricValue: toNumberValue(read(source, 'SecondaryMetricValue', 'secondaryMetricValue')),
    secondaryMetricLabel: toStringValue(read(source, 'SecondaryMetricLabel', 'secondaryMetricLabel'))
});

const toCompetition = (source: unknown): LeaderboardCompetition => ({
    entry: toLeaderboardEntry(read(source, 'Entry', 'entry')),
    gapValue: toNumberValue(read(source, 'GapValue', 'gapValue')),
    gapLabel: toStringValue(read(source, 'GapLabel', 'gapLabel'))
});

const toPersonalStats = (source: unknown): LeaderboardPersonalStats => ({
    seasonYear: toNumberValue(read(source, 'SeasonYear', 'seasonYear')),
    totalXp: toNumberValue(read(source, 'TotalXp', 'totalXp')),
    achievementXp: toNumberValue(read(source, 'AchievementXp', 'achievementXp')),
    achievementCount: toNumberValue(read(source, 'AchievementCount', 'achievementCount')),
    level: toNumberValue(read(source, 'Level', 'level')),
    rank: toNumberValue(read(source, 'Rank', 'rank')),
    percentile: toNumberValue(read(source, 'Percentile', 'percentile')),
    totalUsers: toNumberValue(read(source, 'TotalUsers', 'totalUsers')),
    rankTitle: toStringValue(read(source, 'RankTitle', 'rankTitle')),
    rankEmoji: toStringValue(read(source, 'RankEmoji', 'rankEmoji')),
    totalWatchMinutes: toNumberValue(read(source, 'TotalWatchMinutes', 'totalWatchMinutes')),
    moviesCompleted: toNumberValue(read(source, 'MoviesCompleted', 'moviesCompleted')),
    seriesCompleted: toNumberValue(read(source, 'SeriesCompleted', 'seriesCompleted')),
    uniqueGenresWatched: toNumberValue(read(source, 'UniqueGenresWatched', 'uniqueGenresWatched')),
    currentStreakDays: toNumberValue(read(source, 'CurrentStreakDays', 'currentStreakDays')),
    bestStreakDays: toNumberValue(read(source, 'BestStreakDays', 'bestStreakDays')),
    achievementsUnlocked: toNumberValue(read(source, 'AchievementsUnlocked', 'achievementsUnlocked')),
    approvedRequests: toNumberValue(read(source, 'ApprovedRequests', 'approvedRequests')),
    metricValue: toNumberValue(read(source, 'MetricValue', 'metricValue')),
    metricType: toStringValue(read(source, 'MetricType', 'metricType'), 'xp') as LeaderboardMetricType,
    metricLabel: toStringValue(read(source, 'MetricLabel', 'metricLabel')),
    gapToNext: toNumberValue(read(source, 'GapToNext', 'gapToNext')),
    gapToTop: toNumberValue(read(source, 'GapToTop', 'gapToTop')),
    currentUserEntry: read(source, 'CurrentUserEntry', 'currentUserEntry') ? toLeaderboardEntry(read(source, 'CurrentUserEntry', 'currentUserEntry')) : null,
    nextTarget: read(source, 'NextTarget', 'nextTarget') ? toCompetition(read(source, 'NextTarget', 'nextTarget')) : null,
    behindUser: read(source, 'BehindUser', 'behindUser') ? toCompetition(read(source, 'BehindUser', 'behindUser')) : null
});

const toTopResult = (source: unknown): LeaderboardTopResult => {
    const currentUserPositionRaw = read(source, 'CurrentUserPosition', 'currentUserPosition');
    return {
        seasonYear: toNumberValue(read(source, 'SeasonYear', 'seasonYear')),
        metricType: toStringValue(read(source, 'MetricType', 'metricType'), 'xp') as LeaderboardMetricType,
        entries: toArray(read(source, 'Entries', 'entries')).map(toLeaderboardEntry),
        currentUserPosition: currentUserPositionRaw ? toLeaderboardEntry(currentUserPositionRaw) : null,
        totalUsers: toNumberValue(read(source, 'TotalUsers', 'totalUsers')),
        offset: toNumberValue(read(source, 'Offset', 'offset')),
        limit: toNumberValue(read(source, 'Limit', 'limit')),
        hasMore: toBooleanValue(read(source, 'HasMore', 'hasMore'))
    };
};

export const getLeaderboardPersonal = async (seasonYear?: number, metricType?: LeaderboardMetricType, apiClient?: ApiClient): Promise<LeaderboardPersonalStats> => {
    const client = getApiClient(apiClient);
    const params: Record<string, unknown> = {};
    if (seasonYear) {
        params.seasonYear = seasonYear;
    }

    if (metricType) {
        params.type = metricType;
    }

    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Leaderboard/Me', params),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toPersonalStats(response);
};

export const getLeaderboardTop = async (seasonYear?: number, limit = 10, metricType?: LeaderboardMetricType, apiClient?: ApiClient, offset = 0): Promise<LeaderboardTopResult> => {
    const client = getApiClient(apiClient);
    const params: Record<string, unknown> = { limit, offset };
    if (seasonYear) {
        params.seasonYear = seasonYear;
    }

    if (metricType) {
        params.type = metricType;
    }

    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Leaderboard/Top', params),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toTopResult(response);
};
