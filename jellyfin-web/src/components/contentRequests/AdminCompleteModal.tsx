import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogTitle from '@mui/material/DialogTitle';
import Typography from '@mui/material/Typography';
import type { ApiClient } from 'jellyfin-apiclient';
import React, { type FC, type FormEvent, useCallback, useEffect, useState } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestRow } from 'utils/contentRequestsApi';

interface SearchResultItem {
    Id?: string
    Name?: string
    ProductionYear?: number
}

interface AdminCompleteModalProps {
    open: boolean
    target: ContentRequestRow | null
    apiClient?: ApiClient
    isBusy: boolean
    onClose: () => void
    onConfirm: (requestId: string, jellyfinItemId: string) => Promise<void>
}

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

const AdminCompleteModal: FC<AdminCompleteModalProps> = ({
    open,
    target,
    apiClient,
    isBusy,
    onClose,
    onConfirm
}) => {
    const [ searchTerm, setSearchTerm ] = useState('');
    const [ searchResults, setSearchResults ] = useState<SearchResultItem[]>([]);
    const [ selectedResult, setSelectedResult ] = useState<SearchResultItem | null>(null);
    const [ isSearching, setIsSearching ] = useState(false);
    const [ isConfirming, setIsConfirming ] = useState(false);
    const [ errorMessage, setErrorMessage ] = useState('');

    useEffect(() => {
        if (!open) {
            setSelectedResult(null);
            setIsSearching(false);
            setIsConfirming(false);
            setErrorMessage('');
            return;
        }

        setSearchTerm('');
        setSearchResults([]);
        setSelectedResult(null);
        setIsSearching(false);
        setIsConfirming(false);
        setErrorMessage('');
    }, [ open, target?.id ]);

    const onSearchSubmit = useCallback(async (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        if (!apiClient || !target) {
            return;
        }

        const normalizedSearchTerm = searchTerm.trim();
        if (!normalizedSearchTerm) {
            setSearchResults([]);
            return;
        }

        setIsSearching(true);
        setErrorMessage('');

        try {
            const response = await apiClient.ajax({
                type: 'GET',
                url: apiClient.getUrl('Items', {
                    SearchTerm: normalizedSearchTerm,
                    IncludeItemTypes: target.type === 'Movie' ? 'Movie' : 'Series',
                    Recursive: true,
                    Limit: 30
                }),
                dataType: 'json',
                contentType: 'application/json'
            }) as { Items?: SearchResultItem[] };

            setSearchResults(Array.isArray(response.Items) ? response.Items : []);
        } catch (error) {
            setErrorMessage(await getErrorMessage(error));
        } finally {
            setIsSearching(false);
        }
    }, [ apiClient, searchTerm, target ]);

    const onConfirmComplete = useCallback(async () => {
        const selectedItemId = selectedResult?.Id || '';
        if (!target || !selectedItemId) {
            return;
        }

        setIsConfirming(true);
        setErrorMessage('');
        try {
            await onConfirm(target.id, selectedItemId);
            setSelectedResult(null);
        } catch (error) {
            setErrorMessage(await getErrorMessage(error));
        } finally {
            setIsConfirming(false);
        }
    }, [ onConfirm, selectedResult, target ]);

    const selectedResultTitle = selectedResult?.Name || '';

    return (
        <>
            <Dialog
                open={open}
                onClose={onClose}
                fullWidth
                maxWidth='md'
            >
                <DialogTitle>{globalize.translate('RequestCompleteSearchTitle')}</DialogTitle>
                <DialogContent>
                    <form className='adminCompleteSearchRow' onSubmit={onSearchSubmit}>
                        <input
                            className='requestInput'
                            value={searchTerm}
                            onChange={event => setSearchTerm(event.target.value)}
                            placeholder={globalize.translate('RequestCompleteSearchPlaceholder')}
                            aria-label={globalize.translate('Search')}
                        />
                        <Button type='submit' disabled={isSearching}>
                            {isSearching
                                ? globalize.translate('RequestSearching')
                                : globalize.translate('Search')}
                        </Button>
                    </form>

                    {!!errorMessage && (
                        <Typography className='requestFormMessage requestFormMessageTopSpacing error'>
                            {errorMessage}
                        </Typography>
                    )}

                    <div className='adminCompleteSearchResults'>
                        {searchResults.map(result => {
                            const itemId = result.Id || '';
                            const posterUrl = itemId && apiClient
                                ? apiClient.getScaledImageUrl(itemId, { type: 'Primary', maxHeight: 180 })
                                : '';

                            return (
                                <div className='adminCompleteSearchResult' key={itemId || `${result.Name}-${result.ProductionYear}`}>
                                    <img
                                        className='adminCompleteSearchPoster'
                                        src={posterUrl}
                                        alt=''
                                    />
                                    <div className='adminCompleteResultText'>
                                        <Typography className='adminCompleteResultTitle'>
                                            {result.Name || globalize.translate('Unknown')}
                                        </Typography>
                                        <Typography className='adminCompleteResultYear'>
                                            {result.ProductionYear || '-'}
                                        </Typography>
                                    </div>
                                    <Button
                                        disabled={!itemId}
                                        onClick={() => setSelectedResult(result)}
                                    >
                                        {globalize.translate('Select')}
                                    </Button>
                                </div>
                            );
                        })}
                    </div>
                </DialogContent>
                <DialogActions>
                    <Button onClick={onClose}>
                        {globalize.translate('ButtonCancel')}
                    </Button>
                </DialogActions>
            </Dialog>

            <Dialog
                open={!!selectedResult}
                onClose={() => setSelectedResult(null)}
                fullWidth
                maxWidth='sm'
            >
                <DialogTitle>{globalize.translate('RequestCompleteConfirmTitle')}</DialogTitle>
                <DialogContent>
                    <Typography>
                        {globalize.translate('RequestCompleteConfirmBody', selectedResultTitle)}
                    </Typography>
                </DialogContent>
                <DialogActions>
                    <Button onClick={() => setSelectedResult(null)} color='error'>
                        {globalize.translate('RequestCompleteConfirmNo')}
                    </Button>
                    <Button
                        onClick={onConfirmComplete}
                        variant='contained'
                        disabled={!selectedResult?.Id || isBusy || isConfirming}
                    >
                        {globalize.translate('RequestCompleteConfirmYes')}
                    </Button>
                </DialogActions>
            </Dialog>
        </>
    );
};

export default AdminCompleteModal;
