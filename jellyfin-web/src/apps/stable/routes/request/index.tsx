import React, { type FormEvent, useCallback, useMemo, useState } from 'react';

import layoutManager from 'components/layoutManager';
import RequestForm from 'components/contentRequests/RequestForm';
import RequestHeader from 'components/contentRequests/RequestHeader';
import RequestList from 'components/contentRequests/RequestList';
import RequestPageContainer from 'components/contentRequests/RequestPageContainer';
import RequestQuotaSummary from 'components/contentRequests/RequestQuotaSummary';
import { type RequestSubscriptionUiState } from 'components/contentRequests/types';
import 'components/contentRequests/contentRequests.scss';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import globalize from 'lib/globalize';
import {
    type ContentRequestQuotaSummary,
    type ContentRequestRow,
    type ContentRequestType,
    createContentRequest,
    getMyContentRequests
} from 'utils/contentRequestsApi';
import { isExpiredSubscriptionUser, isInGraceSubscriptionUser } from 'utils/subscription';

import './request.scss';

const defaultQuota: ContentRequestQuotaSummary = {
    cycleStartDate: '',
    isSubscriptionActive: false,
    movieCap: 5,
    seriesCap: 2,
    usedMovies: 0,
    usedSeries: 0,
    remainingMovies: 0,
    remainingSeries: 0
};

const getErrorMessage = async (error: unknown) => {
    if (!error || typeof error !== 'object') {
        return globalize.translate('UnknownError');
    }

    const responseError = error as {
        status?: number
        text?: () => Promise<string>
        response?: {
            data?: unknown
            status?: number
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

    if (responseError.status === 403 || responseError.response?.status === 403) {
        return globalize.translate('RequestSubscriptionInactive');
    }

    return globalize.translate('UnknownError');
};

export const Component = () => {
    const { user } = useApi();

    const [ requestType, setRequestType ] = useState<ContentRequestType>('Movie');
    const [ title, setTitle ] = useState('');
    const [ seasonNumber, setSeasonNumber ] = useState('');
    const [ rows, setRows ] = useState<ContentRequestRow[]>([]);
    const [ quota, setQuota ] = useState<ContentRequestQuotaSummary>(defaultQuota);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSubmitting, setIsSubmitting ] = useState(false);
    const [ formMessage, setFormMessage ] = useState('');
    const [ formError, setFormError ] = useState(false);

    const subscriptionUiState = useMemo<RequestSubscriptionUiState>(() => {
        if (isExpiredSubscriptionUser(user)) {
            return 'expired';
        }

        if (isInGraceSubscriptionUser(user)) {
            return 'grace';
        }

        return 'active';
    }, [ user ]);

    const isQuotaExhausted = useMemo(() => (
        quota.remainingMovies <= 0 && quota.remainingSeries <= 0
    ), [ quota.remainingMovies, quota.remainingSeries ]);

    const isSubmitVisible = subscriptionUiState !== 'expired';
    const isTypeQuotaAvailable = requestType === 'Movie'
        ? quota.remainingMovies > 0
        : quota.remainingSeries > 0;
    const isTypeSelectionEnabled = subscriptionUiState === 'active' && quota.isSubscriptionActive && !isQuotaExhausted;
    const isSubmitEnabled = isTypeSelectionEnabled && isTypeQuotaAvailable;

    const loadData = useCallback(async () => {
        setIsLoading(true);
        try {
            const response = await getMyContentRequests();
            setRows(response.requests);
            setQuota(response.quota);
        } catch (error) {
            setFormError(true);
            setFormMessage(await getErrorMessage(error));
        } finally {
            setIsLoading(false);
        }
    }, []);

    React.useEffect(() => {
        void loadData();
    }, [ loadData ]);

    const onSubmit = useCallback(async (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        if (isSubmitting || !isSubmitEnabled) {
            return;
        }

        const trimmedTitle = title.trim();
        if (!trimmedTitle) {
            setFormError(true);
            setFormMessage(globalize.translate('RequestValidationTitleRequired'));
            return;
        }

        const parsedSeasonNumber = requestType === 'Series' ? Number.parseInt(seasonNumber, 10) : null;
        if (requestType === 'Series' && (parsedSeasonNumber === null || Number.isNaN(parsedSeasonNumber) || parsedSeasonNumber <= 0)) {
            setFormError(true);
            setFormMessage(globalize.translate('RequestValidationSeasonRequired'));
            return;
        }

        setIsSubmitting(true);
        setFormError(false);
        setFormMessage('');

        try {
            await createContentRequest(trimmedTitle, requestType, parsedSeasonNumber);
            setTitle('');
            setSeasonNumber('');
            setFormError(false);
            setFormMessage(globalize.translate('RequestCreatedMessage'));
            await loadData();
        } catch (error) {
            setFormError(true);
            setFormMessage(await getErrorMessage(error));
        } finally {
            setIsSubmitting(false);
        }
    }, [ isSubmitEnabled, isSubmitting, loadData, requestType, seasonNumber, title ]);

    if (isLoading) {
        return <Loading />;
    }

    return (
        <Page
            id='requestPage'
            className='mainAnimatedPage noSecondaryNavPage libraryPage requestPageRoot'
            title={globalize.translate('RequestPageTitle')}
            isBackButtonEnabled={false}
            shouldAutoFocus
        >
            <RequestPageContainer>
                <section className='requestSection requestDisclaimer'>
                    <details open={!layoutManager.tv}>
                        <summary>{globalize.translate('RequestDisclaimerTitle')}</summary>
                        <p>{globalize.translate('RequestDisclaimerBody')}</p>
                    </details>
                </section>

                <div className='requestMainGrid'>
                    <section id='requestCreateSection' className='requestSection'>
                        <RequestHeader title={globalize.translate('RequestFormTitle')} />
                        <RequestQuotaSummary
                            quota={quota}
                            subscriptionUiState={subscriptionUiState}
                            isQuotaExhausted={isQuotaExhausted}
                        />
                        <RequestForm
                            requestType={requestType}
                            title={title}
                            seasonNumber={seasonNumber}
                            isSubmitting={isSubmitting}
                            isSubmitEnabled={isSubmitEnabled}
                            isTypeSelectionEnabled={isTypeSelectionEnabled}
                            isVisible={isSubmitVisible}
                            remainingMovies={quota.remainingMovies}
                            remainingSeries={quota.remainingSeries}
                            message={formMessage}
                            isMessageError={formError}
                            onRequestTypeChange={setRequestType}
                            onTitleChange={setTitle}
                            onSeasonNumberChange={setSeasonNumber}
                            onSubmit={onSubmit}
                        />
                    </section>

                    <RequestList rows={rows} />
                </div>
            </RequestPageContainer>
        </Page>
    );
};

Component.displayName = 'RequestPage';
