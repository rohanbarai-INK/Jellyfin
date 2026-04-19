import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';

export type ContentRequestType = 'Movie' | 'Series';
export type ContentRequestStatus = 'Pending' | 'Approved' | 'Rejected' | 'Completed';

export interface ContentRequestRow {
    id: string
    userId: string
    username: string
    title: string
    type: ContentRequestType
    seasonNumber: number | null
    requestedAt: string
    status: ContentRequestStatus
    jellyfinItemId: string | null
    notificationCount: number
    isAdminViewed: boolean
}

export interface PublicContentRequestRow {
    id: string
    userId: string
    username: string
    title: string
    type: ContentRequestType
    seasonNumber: number | null
    requestedAt: string
    status: ContentRequestStatus
    jellyfinItemId: string | null
}

export interface ContentRequestQuotaSummary {
    cycleStartDate: string
    isSubscriptionActive: boolean
    movieCap: number
    seriesCap: number
    usedMovies: number
    usedSeries: number
    remainingMovies: number
    remainingSeries: number
    rewardMovies: number
    rewardSeries: number
}

export interface MyContentRequestsResult {
    requests: ContentRequestRow[]
    quota: ContentRequestQuotaSummary
}

export interface MyContentRequestsPagedResult {
    items: ContentRequestRow[]
    totalRecordCount: number
    quota: ContentRequestQuotaSummary
}

export interface PublicContentRequestListResult {
    items: PublicContentRequestRow[]
    totalRecordCount: number
}

export interface AdminContentRequestListResult {
    items: ContentRequestRow[]
    totalRecordCount: number
}

export interface ContentRequestUserSuggestion {
    userId: string
    username: string
}

export interface ContentRequestAdminUserQuota {
    userId: string
    username: string
    quota: ContentRequestQuotaSummary
}

export interface ContentRequestWebPushSubscriptionPayload {
    endpoint: string
    p256dh: string
    auth: string
}

type UnknownRecord = Record<string, unknown>;

const enumValueMap = {
    type: {
        0: 'Movie',
        1: 'Series'
    },
    status: {
        0: 'Pending',
        1: 'Approved',
        2: 'Rejected',
        3: 'Completed'
    }
} as const;

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

const toContentRequestType = (value: unknown): ContentRequestType => {
    if (typeof value === 'number') {
        return enumValueMap.type[value as 0 | 1] ?? 'Movie';
    }

    if (typeof value === 'string') {
        const normalized = value.trim().toLowerCase();
        if (normalized === 'series' || normalized === '1') {
            return 'Series';
        }
    }

    return 'Movie';
};

const toContentRequestStatus = (value: unknown): ContentRequestStatus => {
    if (typeof value === 'number') {
        return enumValueMap.status[value as 0 | 1 | 2 | 3] ?? 'Pending';
    }

    if (typeof value === 'string') {
        const normalized = value.trim().toLowerCase();
        if (normalized === 'approved' || normalized === '1') {
            return 'Approved';
        }

        if (normalized === 'rejected' || normalized === '2') {
            return 'Rejected';
        }

        if (normalized === 'completed' || normalized === '3') {
            return 'Completed';
        }
    }

    return 'Pending';
};

const toNumber = (value: unknown, fallback = 0) => (
    typeof value === 'number' && Number.isFinite(value)
        ? value
        : fallback
);

const toStringValue = (value: unknown, fallback = '') => (
    typeof value === 'string' ? value : fallback
);

const toNullableStringValue = (value: unknown) => (
    typeof value === 'string' && value.length > 0 ? value : null
);

const toNullableNumber = (value: unknown) => (
    typeof value === 'number' && Number.isFinite(value) ? value : null
);

const toBooleanValue = (value: unknown, fallback = false) => (
    typeof value === 'boolean' ? value : fallback
);

const toContentRequestRow = (source: unknown): ContentRequestRow => ({
    id: toStringValue(read(source, 'Id', 'id')),
    userId: toStringValue(read(source, 'UserId', 'userId')),
    username: toStringValue(read(source, 'Username', 'username')),
    title: toStringValue(read(source, 'Title', 'title')),
    type: toContentRequestType(read(source, 'Type', 'type')),
    seasonNumber: toNullableNumber(read(source, 'SeasonNumber', 'seasonNumber')),
    requestedAt: toStringValue(read(source, 'RequestedAt', 'requestedAt')),
    status: toContentRequestStatus(read(source, 'Status', 'status')),
    jellyfinItemId: toNullableStringValue(read(source, 'JellyfinItemId', 'jellyfinItemId')),
    notificationCount: toNumber(read(source, 'NotificationCount', 'notificationCount')),
    isAdminViewed: toBooleanValue(read(source, 'IsAdminViewed', 'isAdminViewed'))
});

const toPublicContentRequestRow = (source: unknown): PublicContentRequestRow => ({
    id: toStringValue(read(source, 'Id', 'id')),
    userId: toStringValue(read(source, 'UserId', 'userId')),
    username: toStringValue(read(source, 'Username', 'username')),
    title: toStringValue(read(source, 'Title', 'title')),
    type: toContentRequestType(read(source, 'Type', 'type')),
    seasonNumber: toNullableNumber(read(source, 'SeasonNumber', 'seasonNumber')),
    requestedAt: toStringValue(read(source, 'RequestedAt', 'requestedAt')),
    status: toContentRequestStatus(read(source, 'Status', 'status')),
    jellyfinItemId: toNullableStringValue(read(source, 'JellyfinItemId', 'jellyfinItemId'))
});

const toQuota = (source: unknown): ContentRequestQuotaSummary => ({
    cycleStartDate: toStringValue(read(source, 'CycleStartDate', 'cycleStartDate')),
    isSubscriptionActive: toBooleanValue(read(source, 'IsSubscriptionActive', 'isSubscriptionActive')),
    movieCap: toNumber(read(source, 'MovieCap', 'movieCap'), 5),
    seriesCap: toNumber(read(source, 'SeriesCap', 'seriesCap'), 2),
    usedMovies: toNumber(read(source, 'UsedMovies', 'usedMovies')),
    usedSeries: toNumber(read(source, 'UsedSeries', 'usedSeries')),
    remainingMovies: toNumber(read(source, 'RemainingMovies', 'remainingMovies')),
    remainingSeries: toNumber(read(source, 'RemainingSeries', 'remainingSeries')),
    rewardMovies: toNumber(read(source, 'RewardMovies', 'rewardMovies')),
    rewardSeries: toNumber(read(source, 'RewardSeries', 'rewardSeries'))
});

const toArray = (value: unknown): unknown[] => (
    Array.isArray(value) ? value : []
);

const toUserSuggestion = (source: unknown): ContentRequestUserSuggestion => ({
    userId: toStringValue(read(source, 'UserId', 'userId')),
    username: toStringValue(read(source, 'Username', 'username'))
});

const toAdminUserQuota = (source: unknown): ContentRequestAdminUserQuota => ({
    userId: toStringValue(read(source, 'UserId', 'userId')),
    username: toStringValue(read(source, 'Username', 'username')),
    quota: toQuota(read(source, 'Quota', 'quota'))
});

export const createContentRequest = async (
    title: string,
    type: ContentRequestType,
    seasonNumber?: number | null,
    apiClient?: ApiClient
) => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Request'),
        data: JSON.stringify({
            Title: title,
            Type: type === 'Series' ? 1 : 0,
            SeasonNumber: type === 'Series' ? seasonNumber ?? null : null
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toContentRequestRow(response);
};

export const getMyContentRequests = async (apiClient?: ApiClient): Promise<MyContentRequestsResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/My'),
        dataType: 'json',
        contentType: 'application/json'
    });

    const requests = toArray(read(response, 'Requests', 'requests')).map(toContentRequestRow);
    const quota = toQuota(read(response, 'Quota', 'quota'));

    return {
        requests,
        quota
    };
};

export const getMyContentRequestsPaged = async (
    skip = 0,
    take = 10,
    apiClient?: ApiClient
): Promise<MyContentRequestsPagedResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/My/Paged', {
            skip: Math.max(0, skip),
            take: Math.max(1, take)
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return {
        items: toArray(read(response, 'Items', 'items')).map(toContentRequestRow),
        totalRecordCount: toNumber(read(response, 'TotalRecordCount', 'totalRecordCount')),
        quota: toQuota(read(response, 'Quota', 'quota'))
    };
};

export const getPublicContentRequests = async (
    skip = 0,
    take = 50,
    apiClient?: ApiClient
): Promise<PublicContentRequestListResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Public', {
            skip: Math.max(0, skip),
            take: Math.max(1, take)
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return {
        items: toArray(read(response, 'Items', 'items')).map(toPublicContentRequestRow),
        totalRecordCount: toNumber(read(response, 'TotalRecordCount', 'totalRecordCount'))
    };
};

export const getAdminContentRequests = async (apiClient?: ApiClient): Promise<ContentRequestRow[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Admin'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toContentRequestRow);
};

export const getAdminContentRequestsPaged = async (
    skip = 0,
    take = 10,
    apiClient?: ApiClient
): Promise<AdminContentRequestListResult> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Admin/Paged', {
            skip: Math.max(0, skip),
            take: Math.max(1, take)
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return {
        items: toArray(read(response, 'Items', 'items')).map(toContentRequestRow),
        totalRecordCount: toNumber(read(response, 'TotalRecordCount', 'totalRecordCount'))
    };
};

export const getAdminUnseenPendingCount = async (apiClient?: ApiClient): Promise<number> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Admin/UnseenPendingCount'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toNumber(read(response, 'Count', 'count'));
};

export const searchAdminContentRequestUsers = async (
    query: string,
    take = 8,
    apiClient?: ApiClient
): Promise<ContentRequestUserSuggestion[]> => {
    const client = getApiClient(apiClient);
    const normalizedQuery = query.trim();
    if (!normalizedQuery) {
        return [];
    }

    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Admin/UserSuggestions', {
            query: normalizedQuery,
            take: Math.max(1, take)
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toUserSuggestion);
};

export const getAdminContentRequestUserQuota = async (
    userId: string,
    apiClient?: ApiClient
): Promise<ContentRequestAdminUserQuota> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Admin/UserQuota', {
            userId
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toAdminUserQuota(response);
};

export const grantAdminContentRequestRewardQuota = async (
    userId: string,
    movieCount: number,
    seriesCount: number,
    apiClient?: ApiClient
): Promise<ContentRequestAdminUserQuota> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/Admin/RewardQuota'),
        data: JSON.stringify({
            UserId: userId,
            MovieCount: Math.max(0, Math.trunc(movieCount)),
            SeriesCount: Math.max(0, Math.trunc(seriesCount))
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toAdminUserQuota(response);
};

export const approveContentRequest = async (requestId: string, apiClient?: ApiClient) => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/Admin/Approve'),
        data: JSON.stringify({
            RequestId: requestId
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toContentRequestRow(response);
};

export const rejectContentRequest = async (requestId: string, apiClient?: ApiClient) => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/Admin/Reject'),
        data: JSON.stringify({
            RequestId: requestId
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toContentRequestRow(response);
};

export const completeContentRequest = async (
    requestId: string,
    jellyfinItemId: string,
    apiClient?: ApiClient
) => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/Admin/Complete'),
        data: JSON.stringify({
            RequestId: requestId,
            JellyfinItemId: jellyfinItemId
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toContentRequestRow(response);
};

export const getContentRequestNotifications = async (apiClient?: ApiClient): Promise<ContentRequestRow[]> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/Notifications'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toArray(response).map(toContentRequestRow);
};

export const bulkMarkContentRequestNotificationsViewed = async (
    requestIds: string[],
    apiClient?: ApiClient
) => {
    const client = getApiClient(apiClient);
    await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/NotificationViewedBulk'),
        data: JSON.stringify({
            RequestIds: requestIds
        }),
        dataType: 'json',
        contentType: 'application/json'
    });
};

export const getContentRequestWebPushPublicKey = async (apiClient?: ApiClient): Promise<string> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('Request/WebPush/PublicKey'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toStringValue(read(response, 'PublicKey', 'publicKey'));
};

export const subscribeContentRequestWebPush = async (
    payload: ContentRequestWebPushSubscriptionPayload,
    apiClient?: ApiClient
) => {
    const client = getApiClient(apiClient);
    await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/WebPush/Subscribe'),
        data: JSON.stringify({
            Endpoint: payload.endpoint,
            P256dh: payload.p256dh,
            Auth: payload.auth
        }),
        dataType: 'json',
        contentType: 'application/json'
    });
};

export const unsubscribeContentRequestWebPush = async (
    endpoint: string,
    apiClient?: ApiClient
) => {
    const client = getApiClient(apiClient);
    await client.ajax({
        type: 'POST',
        url: client.getUrl('Request/WebPush/Unsubscribe'),
        data: JSON.stringify({
            Endpoint: endpoint
        }),
        dataType: 'json',
        contentType: 'application/json'
    });
};
