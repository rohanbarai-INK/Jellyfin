import Dialog from '@mui/material/Dialog';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useLocation } from 'react-router-dom';

import layoutManager from 'components/layoutManager';
import { appRouter } from 'components/router/appRouter';
import { useApi } from 'hooks/useApi';
import {
    type ContentRequestRow,
    bulkMarkContentRequestNotificationsViewed,
    getContentRequestNotifications
} from 'utils/contentRequestsApi';
import { isExpiredSubscriptionUser } from 'utils/subscription';

import requestPopupAccentGif from 'assets/branding/request-popup-accent.gif';

import './RequestNotificationPopup.scss';

interface ItemSummary {
    id: string
    title: string
    year: string
    rating: string
    runtime: string
    genres: string
    overview: string
    posterUrl: string
}

interface ApiItem {
    Name?: string
    ProductionYear?: number
    OfficialRating?: string
    CommunityRating?: number
    RunTimeTicks?: number
    Genres?: string[]
    Overview?: string
}

const toMinutesLabel = (runTimeTicks?: number) => {
    if (!runTimeTicks || runTimeTicks <= 0) {
        return '';
    }

    const minutes = Math.max(1, Math.round(runTimeTicks / 600000000));
    return `${minutes}m`;
};

const trimOverview = (value: string) => {
    const normalized = value.trim();
    if (normalized.length <= 260) {
        return normalized;
    }

    return `${normalized.slice(0, 259).trimEnd()}...`;
};

const asRecord = (value: unknown) => (
    value && typeof value === 'object' ? value as Record<string, unknown> : null
);

const isSubscriptionRestrictedError = (error: unknown) => {
    const record = asRecord(error);
    if (!record) {
        return false;
    }

    const xhr = asRecord(record.xhr);
    const response = asRecord(record.response);
    const status = typeof record.status === 'number'
        ? record.status
        : (typeof record.statusCode === 'number'
            ? record.statusCode
            : (typeof xhr?.status === 'number'
                ? xhr.status
                : (typeof response?.status === 'number' ? response.status : null)));

    const message = typeof record.message === 'string' ? record.message : '';
    if (status !== 403 && !message.includes('403')) {
        return false;
    }

    const responseJson = asRecord(record.responseJSON) ?? asRecord(xhr?.responseJSON);
    const responseCode = typeof responseJson?.code === 'string' ? responseJson.code : '';
    if (responseCode.toLowerCase() === 'subscriptionexpired') {
        return true;
    }

    const redirectUrl = typeof responseJson?.redirectUrl === 'string' ? responseJson.redirectUrl : '';
    if (redirectUrl.includes('/subscription')) {
        return true;
    }

    const responseText = typeof record.responseText === 'string'
        ? record.responseText
        : (typeof xhr?.responseText === 'string' ? xhr.responseText : '');

    return responseText.includes('SubscriptionExpired')
        || responseText.includes('/subscription')
        || message.toLowerCase().includes('subscriptionexpired');
};

export const RequestNotificationPopup = () => {
    const location = useLocation();
    const { __legacyApiClient__: apiClient, user } = useApi();
    const [ notifications, setNotifications ] = useState<ContentRequestRow[]>([]);
    const [ summaries, setSummaries ] = useState<Record<string, ItemSummary>>({});
    const [ isOpen, setIsOpen ] = useState(false);

    const activeUserIdRef = useRef<string>('');
    const hasFetchedRef = useRef(false);
    const inFlightRef = useRef(false);

    const popupClassName = useMemo(() => {
        if (layoutManager.tv) {
            return 'requestNotificationDialog tv';
        }

        if (layoutManager.mobile) {
            return 'requestNotificationDialog mobile';
        }

        return 'requestNotificationDialog';
    }, []);

    const fetchItemSummary = useCallback(async (requestRow: ContentRequestRow): Promise<ItemSummary | null> => {
        if (!apiClient || !requestRow.jellyfinItemId) {
            return null;
        }

        try {
            const response = await apiClient.ajax({
                type: 'GET',
                url: apiClient.getUrl(`Items/${requestRow.jellyfinItemId}`),
                dataType: 'json',
                contentType: 'application/json'
            }) as ApiItem;

            const ratingValue = response.OfficialRating
                || (typeof response.CommunityRating === 'number' && Number.isFinite(response.CommunityRating)
                    ? response.CommunityRating.toFixed(1)
                    : '');

            return {
                id: requestRow.jellyfinItemId,
                title: response.Name || requestRow.title,
                year: response.ProductionYear ? String(response.ProductionYear) : '',
                rating: ratingValue,
                runtime: toMinutesLabel(response.RunTimeTicks),
                genres: Array.isArray(response.Genres) ? response.Genres.join(', ') : '',
                overview: trimOverview(response.Overview || ''),
                posterUrl: apiClient.getScaledImageUrl(requestRow.jellyfinItemId, {
                    type: 'Primary',
                    maxHeight: 320
                })
            };
        } catch (error) {
            console.error('[RequestNotificationPopup] failed to fetch item details', error);
            return null;
        }
    }, [ apiClient ]);

    const maybeFetchNotifications = useCallback(async () => {
        if (!apiClient || !user?.Id || hasFetchedRef.current || inFlightRef.current) {
            return;
        }

        if (isExpiredSubscriptionUser(user)) {
            hasFetchedRef.current = true;
            setNotifications([]);
            setSummaries({});
            setIsOpen(false);
            return;
        }

        inFlightRef.current = true;

        try {
            const rows = await getContentRequestNotifications(apiClient);
            hasFetchedRef.current = true;

            if (rows.length === 0) {
                setNotifications([]);
                setSummaries({});
                setIsOpen(false);
                return;
            }

            const summaryEntries = await Promise.all(rows.map(async row => {
                const summary = await fetchItemSummary(row);
                return summary ? [ row.id, summary ] as const : null;
            }));

            const mappedSummaries = summaryEntries.reduce<Record<string, ItemSummary>>((acc, entry) => {
                if (entry) {
                    acc[entry[0]] = entry[1];
                }

                return acc;
            }, {});

            setNotifications(rows);
            setSummaries(mappedSummaries);
            setIsOpen(true);
        } catch (error) {
            hasFetchedRef.current = true;

            if (isSubscriptionRestrictedError(error)) {
                setNotifications([]);
                setSummaries({});
                setIsOpen(false);
                return;
            }

            console.error('[RequestNotificationPopup] failed to fetch notifications', error);
        } finally {
            inFlightRef.current = false;
        }
    }, [ apiClient, fetchItemSummary, user ]);

    useEffect(() => {
        const currentUserId = user?.Id || '';
        if (activeUserIdRef.current === currentUserId) {
            return;
        }

        activeUserIdRef.current = currentUserId;
        hasFetchedRef.current = false;
        inFlightRef.current = false;
        setNotifications([]);
        setSummaries({});
        setIsOpen(false);
    }, [ user?.Id ]);

    useEffect(() => {
        if (user?.Id) {
            void maybeFetchNotifications();
        }
    }, [ maybeFetchNotifications, user?.Id ]);

    useEffect(() => {
        if (location.pathname === '/home' && user?.Id) {
            void maybeFetchNotifications();
        }
    }, [ location.pathname, maybeFetchNotifications, user?.Id ]);

    const onClosePopup = useCallback(() => {
        const requestIds = notifications.map(row => row.id).filter(Boolean);
        setIsOpen(false);

        if (!apiClient || requestIds.length === 0) {
            return;
        }

        void bulkMarkContentRequestNotificationsViewed(requestIds, apiClient)
            .catch(error => {
                console.error('[RequestNotificationPopup] failed to mark notifications viewed', error);
            });
    }, [ apiClient, notifications ]);

    const onWatchNow = useCallback((requestRow: ContentRequestRow) => {
        if (!requestRow.jellyfinItemId || !apiClient) {
            return;
        }

        appRouter.showItem(requestRow.jellyfinItemId, apiClient.serverId());
        onClosePopup();
    }, [ apiClient, onClosePopup ]);

    if (!isOpen || notifications.length === 0) {
        return null;
    }

    return (
        <Dialog
            open
            onClose={onClosePopup}
            className={popupClassName}
            fullWidth
            maxWidth={false}
        >
            <div className='requestNotificationHeader'>
                <div className='requestNotificationHeaderLeft'>
                    {`🎉 ${notifications.length} ${
                        notifications.length === 1 ? 'Title' : 'Titles'
                    } You Requested Are Now Available`}
                </div>
                <div className='requestNotificationHeaderRight'>
                    <img
                        src={requestPopupAccentGif}
                        className='requestNotificationAccent'
                        alt=''
                        aria-hidden='true'
                        role='presentation'
                        tabIndex={-1}
                    />
                    <button
                        type='button'
                        className='requestNotificationCloseButton'
                        onClick={onClosePopup}
                        aria-label='Close'
                    >
                        ×
                    </button>
                </div>
            </div>

            <div className='requestNotificationContent'>
                {notifications.map(row => {
                    const summary = summaries[row.id];
                    const genres = summary?.genres || '';
                    const metadataText = [ summary?.year, summary?.rating, summary?.runtime, genres ]
                        .filter(Boolean)
                        .join(' · ');

                    return (
                        <article className='requestNotificationCard' key={row.id}>
                            <img
                                className='requestNotificationPoster'
                                src={summary?.posterUrl}
                                alt=''
                            />
                            <div>
                                <strong>{summary?.title || row.title}</strong>
                                {!!metadataText && (
                                    <div className='requestNotificationMeta'>{metadataText}</div>
                                )}
                                {!!summary?.overview && (
                                    <p className='requestNotificationOverview'>{summary.overview}</p>
                                )}
                                <div className='requestNotificationActionRow'>
                                    <button
                                        type='button'
                                        className='requestNotificationWatchButton'
                                        onClick={() => onWatchNow(row)}
                                        disabled={!row.jellyfinItemId}
                                    >
                                        ▶ Watch Now
                                    </button>
                                </div>
                            </div>
                        </article>
                    );
                })}
            </div>
        </Dialog>
    );
};

export default RequestNotificationPopup;

