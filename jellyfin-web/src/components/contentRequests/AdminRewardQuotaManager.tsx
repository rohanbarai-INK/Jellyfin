import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Typography from '@mui/material/Typography';
import type { ApiClient } from 'jellyfin-apiclient';
import React, { type FC, useCallback, useEffect, useMemo, useRef, useState } from 'react';

import toast from 'components/toast/toast';
import globalize from 'lib/globalize';
import {
    type ContentRequestAdminUserQuota,
    type ContentRequestUserSuggestion,
    getAdminContentRequestUserQuota,
    grantAdminContentRequestRewardQuota,
    searchAdminContentRequestUsers
} from 'utils/contentRequestsApi';

interface AdminRewardQuotaManagerProps {
    apiClient?: ApiClient
    isBusy: boolean
    onRewardApplied: () => Promise<void>
}

const SUGGESTION_DEBOUNCE_MS = 2000;
const QUICK_ADD_OPTIONS = [ 1, 2, 3, 5, 10, 20 ];

const getErrorMessage = async (error: unknown) => {
    if (!error || typeof error !== 'object') {
        return globalize.translate('UnknownError');
    }

    const responseError = error as {
        text?: () => Promise<string>
        response?: {
            data?: unknown
        }
    };

    const responseData = responseError.response?.data;
    if (typeof responseData === 'string' && responseData.trim()) {
        return responseData;
    }

    if (responseData && typeof responseData === 'object') {
        const payload = responseData as Record<string, unknown>;
        const message = payload.message ?? payload.Message ?? payload.error ?? payload.Error;
        if (typeof message === 'string' && message.trim()) {
            return message;
        }
    }

    if (typeof responseError.text === 'function') {
        const text = await responseError.text();
        if (text.trim()) {
            return text;
        }
    }

    return globalize.translate('UnknownError');
};

const toPositiveInt = (value: string) => {
    const parsed = Number.parseInt(value, 10);
    if (!Number.isFinite(parsed) || parsed <= 0) {
        return 0;
    }

    return parsed;
};

const AdminRewardQuotaManager: FC<AdminRewardQuotaManagerProps> = ({
    apiClient,
    isBusy,
    onRewardApplied
}) => {
    const [ usernameQuery, setUsernameQuery ] = useState('');
    const [ suggestions, setSuggestions ] = useState<ContentRequestUserSuggestion[]>([]);
    const [ isSuggesting, setIsSuggesting ] = useState(false);
    const [ selectedUser, setSelectedUser ] = useState<ContentRequestUserSuggestion | null>(null);
    const [ userQuota, setUserQuota ] = useState<ContentRequestAdminUserQuota | null>(null);
    const [ isLoadingQuota, setIsLoadingQuota ] = useState(false);
    const [ movieCountToAdd, setMovieCountToAdd ] = useState('');
    const [ seriesCountToAdd, setSeriesCountToAdd ] = useState('');
    const [ isSubmitting, setIsSubmitting ] = useState(false);
    const [ isConfirmOpen, setIsConfirmOpen ] = useState(false);
    const suggestionRequestRef = useRef(0);

    const normalizedQuery = usernameQuery.trim();
    const parsedMovieCountToAdd = useMemo(() => toPositiveInt(movieCountToAdd), [ movieCountToAdd ]);
    const parsedSeriesCountToAdd = useMemo(() => toPositiveInt(seriesCountToAdd), [ seriesCountToAdd ]);
    const hasCountsToAdd = parsedMovieCountToAdd > 0 || parsedSeriesCountToAdd > 0;
    const canAddRewards = !!selectedUser && hasCountsToAdd && !isSubmitting && !isBusy;

    const resetSelectionState = useCallback(() => {
        setSelectedUser(null);
        setUserQuota(null);
        setMovieCountToAdd('');
        setSeriesCountToAdd('');
    }, []);

    const loadUserQuota = useCallback(async (userId: string) => {
        setIsLoadingQuota(true);
        try {
            const result = await getAdminContentRequestUserQuota(userId, apiClient);
            setUserQuota(result);
        } catch (error) {
            setUserQuota(null);
            toast(await getErrorMessage(error));
        } finally {
            setIsLoadingQuota(false);
        }
    }, [ apiClient ]);

    useEffect(() => {
        const selectedUsername = selectedUser?.username.trim().toLowerCase();
        if (!normalizedQuery || (selectedUsername && normalizedQuery.toLowerCase() === selectedUsername)) {
            setSuggestions([]);
            setIsSuggesting(false);
            return;
        }

        const requestId = suggestionRequestRef.current + 1;
        suggestionRequestRef.current = requestId;
        setIsSuggesting(true);

        const timeoutId = window.setTimeout(async () => {
            try {
                const rows = await searchAdminContentRequestUsers(normalizedQuery, 8, apiClient);
                if (suggestionRequestRef.current === requestId) {
                    setSuggestions(rows);
                }
            } catch (error) {
                if (suggestionRequestRef.current === requestId) {
                    setSuggestions([]);
                    toast(await getErrorMessage(error));
                }
            } finally {
                if (suggestionRequestRef.current === requestId) {
                    setIsSuggesting(false);
                }
            }
        }, SUGGESTION_DEBOUNCE_MS);

        return () => {
            window.clearTimeout(timeoutId);
        };
    }, [ apiClient, normalizedQuery, selectedUser?.username ]);

    const onUsernameInputChange = useCallback((value: string) => {
        setUsernameQuery(value);
        resetSelectionState();
    }, [ resetSelectionState ]);

    const onSelectUserSuggestion = useCallback((suggestion: ContentRequestUserSuggestion) => {
        setSelectedUser(suggestion);
        setUsernameQuery(suggestion.username);
        setSuggestions([]);
        void loadUserQuota(suggestion.userId);
    }, [ loadUserQuota ]);

    const onOpenConfirm = useCallback(() => {
        if (!canAddRewards) {
            return;
        }

        setIsConfirmOpen(true);
    }, [ canAddRewards ]);

    const onConfirmAddRewards = useCallback(async () => {
        if (!selectedUser || !hasCountsToAdd) {
            return;
        }

        setIsSubmitting(true);
        try {
            const result = await grantAdminContentRequestRewardQuota(
                selectedUser.userId,
                parsedMovieCountToAdd,
                parsedSeriesCountToAdd,
                apiClient
            );

            setUserQuota(result);
            setMovieCountToAdd('');
            setSeriesCountToAdd('');
            setIsConfirmOpen(false);

            toast(globalize.translate('RequestAdminRewardSuccessToast'));
            await onRewardApplied();
        } catch (error) {
            toast(await getErrorMessage(error));
        } finally {
            setIsSubmitting(false);
        }
    }, [
        apiClient,
        hasCountsToAdd,
        onRewardApplied,
        parsedMovieCountToAdd,
        parsedSeriesCountToAdd,
        selectedUser
    ]);

    return (
        <>
            <div className='adminRewardGrid'>
                <label className='requestFilterField adminRewardUserField'>
                    <span>{globalize.translate('RequestAdminRewardUserLabel')}</span>
                    <input
                        className='requestInput'
                        type='text'
                        value={usernameQuery}
                        onChange={event => onUsernameInputChange(event.target.value)}
                        placeholder={globalize.translate('RequestAdminRewardUserPlaceholder')}
                        aria-label={globalize.translate('RequestAdminRewardUserLabel')}
                    />
                </label>

                <div className='adminRewardSuggestMeta'>
                    {isSuggesting
                        ? globalize.translate('RequestAdminRewardSuggesting')
                        : globalize.translate('RequestAdminRewardSuggestHint')}
                </div>

                {suggestions.length > 0 && (
                    <div className='adminRewardSuggestions' role='listbox' aria-label={globalize.translate('RequestAdminRewardSuggestionsLabel')}>
                        {suggestions.map(suggestion => (
                            <button
                                key={suggestion.userId}
                                type='button'
                                className='adminRewardSuggestionButton'
                                onClick={() => onSelectUserSuggestion(suggestion)}
                            >
                                {suggestion.username}
                            </button>
                        ))}
                    </div>
                )}

                <div className='adminRewardQuotaPanel'>
                    <div className='adminRewardQuotaHeader'>
                        <strong>{globalize.translate('RequestAdminRewardQuotaTitle')}</strong>
                        {isLoadingQuota && <span>{globalize.translate('RequestAdminRewardLoadingQuota')}</span>}
                    </div>
                    {userQuota && (
                        <div className='adminRewardQuotaStats'>
                            <div>
                                <span>{globalize.translate('RequestAdminRewardRemainingMovies')}</span>
                                <strong>{userQuota.quota.remainingMovies}</strong>
                            </div>
                            <div>
                                <span>{globalize.translate('RequestAdminRewardRemainingSeries')}</span>
                                <strong>{userQuota.quota.remainingSeries}</strong>
                            </div>
                            <div>
                                <span>{globalize.translate('RequestAdminRewardBonusMovies')}</span>
                                <strong>{userQuota.quota.rewardMovies}</strong>
                            </div>
                            <div>
                                <span>{globalize.translate('RequestAdminRewardBonusSeries')}</span>
                                <strong>{userQuota.quota.rewardSeries}</strong>
                            </div>
                        </div>
                    )}
                    {!userQuota && !isLoadingQuota && (
                        <div className='adminRewardQuotaEmpty'>
                            {globalize.translate('RequestAdminRewardSelectUserHint')}
                        </div>
                    )}
                </div>

                <div className='adminRewardInputGrid'>
                    <label className='requestFilterField'>
                        <span>{globalize.translate('RequestAdminRewardMovieAddLabel')}</span>
                        <div className='adminRewardInlineInput'>
                            <input
                                className='requestInput'
                                type='number'
                                min={0}
                                step={1}
                                value={movieCountToAdd}
                                onChange={event => setMovieCountToAdd(event.target.value)}
                                placeholder='0'
                                aria-label={globalize.translate('RequestAdminRewardMovieAddLabel')}
                            />
                            <select
                                className='requestInput requestSelect adminRewardQuickSelect'
                                value=''
                                onChange={event => setMovieCountToAdd(event.target.value)}
                                aria-label={globalize.translate('RequestAdminRewardQuickPickLabel')}
                            >
                                <option value=''>{globalize.translate('RequestAdminRewardQuickPickLabel')}</option>
                                {QUICK_ADD_OPTIONS.map(option => (
                                    <option key={`movie-quick-${option}`} value={option.toString()}>{option}</option>
                                ))}
                            </select>
                        </div>
                    </label>

                    <label className='requestFilterField'>
                        <span>{globalize.translate('RequestAdminRewardSeriesAddLabel')}</span>
                        <div className='adminRewardInlineInput'>
                            <input
                                className='requestInput'
                                type='number'
                                min={0}
                                step={1}
                                value={seriesCountToAdd}
                                onChange={event => setSeriesCountToAdd(event.target.value)}
                                placeholder='0'
                                aria-label={globalize.translate('RequestAdminRewardSeriesAddLabel')}
                            />
                            <select
                                className='requestInput requestSelect adminRewardQuickSelect'
                                value=''
                                onChange={event => setSeriesCountToAdd(event.target.value)}
                                aria-label={globalize.translate('RequestAdminRewardQuickPickLabel')}
                            >
                                <option value=''>{globalize.translate('RequestAdminRewardQuickPickLabel')}</option>
                                {QUICK_ADD_OPTIONS.map(option => (
                                    <option key={`series-quick-${option}`} value={option.toString()}>{option}</option>
                                ))}
                            </select>
                        </div>
                    </label>
                </div>

                <div className='adminRewardActionRow'>
                    <button
                        type='button'
                        className='requestSubmitButton'
                        disabled={!canAddRewards}
                        onClick={onOpenConfirm}
                    >
                        {globalize.translate('RequestAdminRewardAddButton')}
                    </button>
                </div>
            </div>

            <Dialog
                open={isConfirmOpen}
                onClose={() => setIsConfirmOpen(false)}
                fullWidth
                maxWidth='sm'
            >
                <DialogTitle>{globalize.translate('RequestAdminRewardConfirmTitle')}</DialogTitle>
                <DialogContent>
                    <Typography>
                        {globalize.translate(
                            'RequestAdminRewardConfirmBody',
                            selectedUser?.username || '-',
                            parsedMovieCountToAdd.toString(),
                            parsedSeriesCountToAdd.toString())}
                    </Typography>
                </DialogContent>
                <DialogActions>
                    <Button onClick={() => setIsConfirmOpen(false)}>
                        {globalize.translate('ButtonCancel')}
                    </Button>
                    <Button
                        onClick={onConfirmAddRewards}
                        variant='contained'
                        disabled={!canAddRewards || isSubmitting}
                    >
                        {globalize.translate('RequestCompleteConfirmYes')}
                    </Button>
                </DialogActions>
            </Dialog>
        </>
    );
};

export default AdminRewardQuotaManager;
