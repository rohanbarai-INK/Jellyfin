import type { Api } from '@jellyfin/sdk';

import staticBannerDark from 'assets/branding/banner-dark.png';
import staticBannerLight from 'assets/branding/banner-light.png';
import staticLogo from 'assets/branding/icon-transparent.png';
import { LOGO_URL } from 'constants/branding';

const customLogoAvailability = new Map<string, boolean>();

const getApiCacheKey = (api: Api) => api.getUri(LOGO_URL);

const toCssUrl = (url: string) => `url("${url}")`;

const setStaticCssVariables = () => {
    const root = document.documentElement;
    root.style.setProperty('--jf-brand-splash-logo', toCssUrl(staticLogo));
    root.style.setProperty('--jf-brand-splash-banner', toCssUrl(staticBannerLight));
    root.style.setProperty('--jf-brand-header-banner-light', toCssUrl(staticBannerLight));
    root.style.setProperty('--jf-brand-header-banner-dark', toCssUrl(staticBannerDark));
    root.style.setProperty('--jf-brand-header-tv-logo', toCssUrl(staticLogo));
};

const setDynamicCssVariables = (logoUrl: string) => {
    const root = document.documentElement;
    const cssUrl = toCssUrl(logoUrl);
    root.style.setProperty('--jf-brand-splash-logo', cssUrl);
    root.style.setProperty('--jf-brand-splash-banner', cssUrl);
    root.style.setProperty('--jf-brand-header-banner-light', cssUrl);
    root.style.setProperty('--jf-brand-header-banner-dark', cssUrl);
    root.style.setProperty('--jf-brand-header-tv-logo', cssUrl);
};

export const getStaticLogoUrl = () => staticLogo;

export const invalidateBrandLogoCache = (api?: Api) => {
    if (!api) {
        customLogoAvailability.clear();
        return;
    }

    customLogoAvailability.delete(getApiCacheKey(api));
};

export const hasCustomLogo = async (api?: Api, force = false): Promise<boolean> => {
    if (!api) {
        return false;
    }

    const cacheKey = getApiCacheKey(api);
    if (!force && customLogoAvailability.has(cacheKey)) {
        return customLogoAvailability.get(cacheKey)!;
    }

    const testUrl = api.getUri(LOGO_URL, { t: Date.now() });
    try {
        const response = await fetch(testUrl, {
            method: 'GET',
            credentials: 'include',
            cache: 'no-store'
        });

        customLogoAvailability.set(cacheKey, response.ok);
        return response.ok;
    } catch {
        customLogoAvailability.set(cacheKey, false);
        return false;
    }
};

export const resolveBrandLogoUrl = async (api?: Api, force = false): Promise<string> => {
    if (!api) {
        return staticLogo;
    }

    const customEnabled = await hasCustomLogo(api, force);
    return customEnabled ? api.getUri(LOGO_URL, { t: Date.now() }) : staticLogo;
};

export const applyBrandLogoCssVariables = async (api?: Api, force = false): Promise<string> => {
    const logoUrl = await resolveBrandLogoUrl(api, force);

    if (api && logoUrl !== staticLogo) {
        setDynamicCssVariables(logoUrl);
    } else {
        setStaticCssVariables();
    }

    return logoUrl;
};

