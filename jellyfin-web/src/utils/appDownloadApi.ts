import type { ApiClient } from 'jellyfin-apiclient';

import { ServerConnections } from 'lib/jellyfin-apiclient';

export interface AppDownloadConfig {
    mobileApkUrl: string
    mobileApkFileName: string
    mobileIsNew: boolean
    tvApkUrl: string
    tvApkFileName: string
    tvIsNew: boolean
    maxNewInteractions: number
    updatedAtUtc: string
    updatedByUsername: string
}

export interface SaveAppDownloadConfigPayload {
    mobileApkUrl: string
    mobileApkFileName: string
    mobileIsNew: boolean
    tvApkUrl: string
    tvApkFileName: string
    tvIsNew: boolean
    maxNewInteractions: number
}

const read = (source: unknown, ...keys: string[]): unknown => {
    if (!source || typeof source !== 'object') {
        return undefined;
    }

    const obj = source as Record<string, unknown>;
    for (const key of keys) {
        if (key in obj) {
            return obj[key];
        }
    }

    return undefined;
};

const toStr = (value: unknown, fallback = ''): string => (
    typeof value === 'string' ? value : fallback
);

const toBool = (value: unknown, fallback = false): boolean => (
    typeof value === 'boolean' ? value : fallback
);

const toNum = (value: unknown, fallback = 0): number => {
    if (typeof value === 'number' && Number.isFinite(value)) return value;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

const toAppDownloadConfig = (source: unknown): AppDownloadConfig => ({
    mobileApkUrl: toStr(read(source, 'MobileApkUrl', 'mobileApkUrl')),
    mobileApkFileName: toStr(read(source, 'MobileApkFileName', 'mobileApkFileName'), 'KnightFlix-v0.0.1.apk'),
    mobileIsNew: toBool(read(source, 'MobileIsNew', 'mobileIsNew')),
    tvApkUrl: toStr(read(source, 'TvApkUrl', 'tvApkUrl')),
    tvApkFileName: toStr(read(source, 'TvApkFileName', 'tvApkFileName'), 'KnightFlixTV-v0.0.1.apk'),
    tvIsNew: toBool(read(source, 'TvIsNew', 'tvIsNew')),
    maxNewInteractions: Math.max(1, toNum(read(source, 'MaxNewInteractions', 'maxNewInteractions'), 3)),
    updatedAtUtc: toStr(read(source, 'UpdatedAtUtc', 'updatedAtUtc')),
    updatedByUsername: toStr(read(source, 'UpdatedByUsername', 'updatedByUsername'))
});

const getApiClient = (apiClient?: ApiClient) => {
    const resolved = apiClient || ServerConnections.currentApiClient();
    if (!resolved) {
        throw new Error('No API client available.');
    }

    return resolved;
};

export const getAppDownloadConfig = async (apiClient?: ApiClient): Promise<AppDownloadConfig> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'GET',
        url: client.getUrl('AppDownload/Config'),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toAppDownloadConfig(response);
};

export const saveAppDownloadConfig = async (
    payload: SaveAppDownloadConfigPayload,
    apiClient?: ApiClient
): Promise<AppDownloadConfig> => {
    const client = getApiClient(apiClient);
    const response = await client.ajax({
        type: 'POST',
        url: client.getUrl('AppDownload/Config'),
        data: JSON.stringify({
            MobileApkUrl: payload.mobileApkUrl,
            MobileApkFileName: payload.mobileApkFileName,
            MobileIsNew: payload.mobileIsNew,
            TvApkUrl: payload.tvApkUrl,
            TvApkFileName: payload.tvApkFileName,
            TvIsNew: payload.tvIsNew,
            MaxNewInteractions: payload.maxNewInteractions
        }),
        dataType: 'json',
        contentType: 'application/json'
    });

    return toAppDownloadConfig(response);
};
