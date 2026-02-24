export function normalizeServerUrl(url) {
    return (url || '').trim().replace(/\/+$/, '');
}

export const HARDCODED_SERVER_URL = normalizeServerUrl(process.env.JELLYFIN_SERVER_URL ?? '');
export const IS_HARDCODED_SERVER_MODE = HARDCODED_SERVER_URL.length > 0;
