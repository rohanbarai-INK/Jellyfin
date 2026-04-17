import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

import { appRouter } from 'components/router/appRouter';
import { useApi } from 'hooks/useApi';
import { fetchActiveFeatureAnnouncementCampaigns } from 'utils/featureAnnouncementsApi';

import { FEATURE_ANNOUNCEMENT_CAMPAIGNS } from './featureAnnouncementCampaigns';
import {
    getEligibleCampaigns,
    recordCampaignImpression
} from './featureAnnouncementImpressionStore';
import type { FeatureAnnouncementCampaign } from './featureAnnouncementTypes';
import FeatureAnnouncementPopup from './FeatureAnnouncementPopup';

const SESSION_STORAGE_KEY_PREFIX = 'jellyfin.featureAnnouncements.session.v1';

const BLOCKED_PATH_PREFIXES = [
    '/login',
    '/selectserver',
    '/addserver',
    '/wizard',
    '/quickconnect',
    '/forgotpassword',
    '/forgotpasswordpin'
];

const canDisplayOnPath = (pathName: string) => !BLOCKED_PATH_PREFIXES
    .some(prefix => pathName.startsWith(prefix));

const getServerScopeId = (serverIdFromUser: string | null, serverIdFromClient: string | null) => (
    serverIdFromUser || serverIdFromClient || 'default'
);

const getSessionStorageKey = (scopeKey: string) => `${SESSION_STORAGE_KEY_PREFIX}:${scopeKey}`;

const safeGetSessionStorageItem = (key: string) => {
    try {
        return window.sessionStorage.getItem(key);
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to read sessionStorage key', error);
        return null;
    }
};

const safeSetSessionStorageItem = (key: string, value: string) => {
    try {
        window.sessionStorage.setItem(key, value);
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to write sessionStorage key', error);
    }
};

const loadSessionShownCampaignIds = (scopeKey: string) => {
    if (!scopeKey) {
        return new Set<string>();
    }

    const rawValue = safeGetSessionStorageItem(getSessionStorageKey(scopeKey));
    if (!rawValue) {
        return new Set<string>();
    }

    try {
        const parsed = JSON.parse(rawValue) as string[];
        if (!Array.isArray(parsed)) {
            return new Set<string>();
        }

        return new Set<string>(parsed.filter(Boolean));
    } catch (error) {
        console.warn('[FeatureAnnouncements] Failed to parse session shown campaigns', error);
        return new Set<string>();
    }
};

const saveSessionShownCampaignIds = (scopeKey: string, campaignIds: Set<string>) => {
    if (!scopeKey) {
        return;
    }

    safeSetSessionStorageItem(getSessionStorageKey(scopeKey), JSON.stringify([ ...campaignIds ]));
};

const FeatureAnnouncementsRoot = () => {
    const location = useLocation();
    const { user, __legacyApiClient__: apiClient } = useApi();

    const [ availableCampaigns, setAvailableCampaigns ] = useState<FeatureAnnouncementCampaign[]>([]);
    const [ campaignsReady, setCampaignsReady ] = useState(false);
    const [ activeCampaigns, setActiveCampaigns ] = useState<FeatureAnnouncementCampaign[]>([]);
    const [ activeCampaignIndex, setActiveCampaignIndex ] = useState(0);

    const activeCampaign = activeCampaigns[activeCampaignIndex] || null;

    const shownThisSessionRef = useRef<Set<string>>(new Set());
    const activeUserScopeRef = useRef<string>('');
    const activeServerIdRef = useRef<string>('default');
    const activeUserIdRef = useRef<string>('');

    const openCampaignSlide = useCallback((nextIndex: number) => {
        if (nextIndex < 0 || nextIndex >= activeCampaigns.length) {
            return;
        }

        const nextCampaign = activeCampaigns[nextIndex];
        const userId = activeUserIdRef.current;
        const serverId = activeServerIdRef.current;

        if (userId && serverId && !shownThisSessionRef.current.has(nextCampaign.id)) {
            recordCampaignImpression(userId, serverId, nextCampaign.id, new Date().toISOString());
            shownThisSessionRef.current.add(nextCampaign.id);
            saveSessionShownCampaignIds(activeUserScopeRef.current, shownThisSessionRef.current);
        }

        setActiveCampaignIndex(nextIndex);
    }, [ activeCampaigns ]);

    const tryOpenCampaign = useCallback(() => {
        const userId = user?.Id || '';
        if (!userId || activeCampaign || !campaignsReady || !canDisplayOnPath(location.pathname)) {
            return;
        }

        const serverId = activeServerIdRef.current;
        const eligibleCampaigns = getEligibleCampaigns({
            campaigns: availableCampaigns,
            userId,
            serverId,
            now: new Date(),
            sessionShownCampaignIds: shownThisSessionRef.current
        });

        if (!eligibleCampaigns.length) {
            return;
        }

        const firstCampaign = eligibleCampaigns[0];
        recordCampaignImpression(userId, serverId, firstCampaign.id, new Date().toISOString());
        shownThisSessionRef.current.add(firstCampaign.id);
        saveSessionShownCampaignIds(activeUserScopeRef.current, shownThisSessionRef.current);
        setActiveCampaigns(eligibleCampaigns);
        setActiveCampaignIndex(0);
    }, [ activeCampaign, availableCampaigns, campaignsReady, location.pathname, user?.Id ]);

    useEffect(() => {
        let isCancelled = false;

        if (!user?.Id) {
            setAvailableCampaigns(FEATURE_ANNOUNCEMENT_CAMPAIGNS);
            setCampaignsReady(true);
            return () => {
                isCancelled = true;
            };
        }

        setCampaignsReady(false);
        void (async () => {
            try {
                const campaignsFromApi = await fetchActiveFeatureAnnouncementCampaigns(apiClient || undefined);
                if (isCancelled) {
                    return;
                }

                setAvailableCampaigns(campaignsFromApi);
            } catch (error) {
                console.warn('[FeatureAnnouncements] Failed to load campaigns from API, using local fallback.', error);
                if (!isCancelled) {
                    setAvailableCampaigns(FEATURE_ANNOUNCEMENT_CAMPAIGNS);
                }
            } finally {
                if (!isCancelled) {
                    setCampaignsReady(true);
                }
            }
        })();

        return () => {
            isCancelled = true;
        };
    }, [ apiClient, user?.Id ]);

    useEffect(() => {
        const userId = user?.Id || '';
        const serverIdFromUser = user?.ServerId || null;
        const serverIdFromClient = typeof apiClient?.serverId === 'function' ?
            (apiClient.serverId() || null) :
            null;
        const resolvedServerId = getServerScopeId(serverIdFromUser, serverIdFromClient);
        const scopeKey = userId ? `${resolvedServerId}:${userId}` : '';

        if (activeUserScopeRef.current === scopeKey) {
            return;
        }

        activeUserScopeRef.current = scopeKey;
        activeUserIdRef.current = userId;
        activeServerIdRef.current = resolvedServerId;
        shownThisSessionRef.current = loadSessionShownCampaignIds(scopeKey);
        setActiveCampaigns([]);
        setActiveCampaignIndex(0);
    }, [ apiClient, user?.Id, user?.ServerId ]);

    useEffect(() => {
        if (!user?.Id) {
            setActiveCampaigns([]);
            setActiveCampaignIndex(0);
            return;
        }

        const timer = window.setTimeout(() => {
            tryOpenCampaign();
        }, 350);

        return () => {
            window.clearTimeout(timer);
        };
    }, [ campaignsReady, location.pathname, tryOpenCampaign, user?.Id ]);

    const onClose = useCallback(() => {
        setActiveCampaigns([]);
        setActiveCampaignIndex(0);
    }, []);

    const onCheckItOut = useCallback(() => {
        if (!activeCampaign) {
            return;
        }

        if (activeCampaign.ctaTargetType === 'external') {
            try {
                const targetUrl = new URL(activeCampaign.ctaRoute);
                window.location.assign(targetUrl.toString());
            } catch (error) {
                console.warn('[FeatureAnnouncements] Invalid external CTA target', error);
            }

            setActiveCampaigns([]);
            setActiveCampaignIndex(0);
            return;
        }

        void appRouter.show(activeCampaign.ctaRoute);
        setActiveCampaigns([]);
        setActiveCampaignIndex(0);
    }, [ activeCampaign ]);

    const onPreviousSlide = useCallback(() => {
        openCampaignSlide(activeCampaignIndex - 1);
    }, [ activeCampaignIndex, openCampaignSlide ]);

    const onNextSlide = useCallback(() => {
        openCampaignSlide(activeCampaignIndex + 1);
    }, [ activeCampaignIndex, openCampaignSlide ]);

    if (!activeCampaign) {
        return null;
    }

    return (
        <FeatureAnnouncementPopup
            campaign={activeCampaign}
            onCheckItOut={onCheckItOut}
            onClose={onClose}
            slideIndex={activeCampaignIndex}
            slideCount={activeCampaigns.length}
            onPreviousSlide={onPreviousSlide}
            onNextSlide={onNextSlide}
        />
    );
};

export default FeatureAnnouncementsRoot;
