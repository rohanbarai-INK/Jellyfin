import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

import { appRouter } from 'components/router/appRouter';
import { useApi } from 'hooks/useApi';

import { FEATURE_ANNOUNCEMENT_CAMPAIGNS } from './featureAnnouncementCampaigns';
import {
    getNextEligibleCampaign,
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

    const [ activeCampaign, setActiveCampaign ] = useState<FeatureAnnouncementCampaign | null>(null);
    const shownThisSessionRef = useRef<Set<string>>(new Set());
    const activeUserScopeRef = useRef<string>('');

    const tryOpenCampaign = useCallback(() => {
        const userId = user?.Id || '';
        if (!userId || activeCampaign || !canDisplayOnPath(location.pathname)) {
            return;
        }

        const serverIdFromUser = user?.ServerId || null;
        const serverIdFromClient = typeof apiClient?.serverId === 'function' ?
            (apiClient.serverId() || null) :
            null;

        const serverId = getServerScopeId(serverIdFromUser, serverIdFromClient);
        const nextCampaign = getNextEligibleCampaign({
            campaigns: FEATURE_ANNOUNCEMENT_CAMPAIGNS,
            userId,
            serverId,
            now: new Date(),
            sessionShownCampaignIds: shownThisSessionRef.current
        });

        if (!nextCampaign) {
            return;
        }

        recordCampaignImpression(userId, serverId, nextCampaign.id, new Date().toISOString());
        shownThisSessionRef.current.add(nextCampaign.id);
        saveSessionShownCampaignIds(activeUserScopeRef.current, shownThisSessionRef.current);
        setActiveCampaign(nextCampaign);
    }, [ activeCampaign, apiClient, location.pathname, user?.Id, user?.ServerId ]);

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
        shownThisSessionRef.current = loadSessionShownCampaignIds(scopeKey);
        setActiveCampaign(null);
    }, [ apiClient, user?.Id, user?.ServerId ]);

    useEffect(() => {
        if (!user?.Id) {
            setActiveCampaign(null);
            return;
        }

        const timer = window.setTimeout(() => {
            tryOpenCampaign();
        }, 350);

        return () => {
            window.clearTimeout(timer);
        };
    }, [ location.pathname, tryOpenCampaign, user?.Id ]);

    const onClose = useCallback(() => {
        setActiveCampaign(null);
    }, []);

    const onCheckItOut = useCallback(() => {
        if (!activeCampaign) {
            return;
        }

        void appRouter.show(activeCampaign.ctaRoute);
        setActiveCampaign(null);
    }, [ activeCampaign ]);

    if (!activeCampaign) {
        return null;
    }

    return (
        <FeatureAnnouncementPopup
            campaign={activeCampaign}
            onCheckItOut={onCheckItOut}
            onClose={onClose}
        />
    );
};

export default FeatureAnnouncementsRoot;
