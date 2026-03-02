import React, { type FormEvent, useCallback, useMemo, useState } from 'react';

import RequestForm from 'components/contentRequests/RequestForm';
import RequestHeader from 'components/contentRequests/RequestHeader';
import RequestList from 'components/contentRequests/RequestList';
import RequestPageContainer from 'components/contentRequests/RequestPageContainer';
import RequestQuotaSummary from 'components/contentRequests/RequestQuotaSummary';
import { type RequestSubscriptionUiState } from 'components/contentRequests/types';
import { syncAchievementsAndShow } from 'components/rewardSystem/AchievementOverlayMount';
import {
    getActivityRewardTotals,
    subscribeActivityRewardHistory
} from 'components/rewardSystem/activityRewardHistoryStore';
import {
    addSpentCoins,
    getSpentCoins
} from 'components/rewardSystem/coinSpendStore';
import Coin from 'components/rewardSystem/Coin';
import 'components/contentRequests/contentRequests.scss';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import globalize from 'lib/globalize';
import { getAchievementHistory as getAchievementHistoryApi } from 'utils/achievementsApi';
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

const MOVIE_REDEEM_COINS = 200;
const SERIES_REDEEM_COINS = 400;
const INSUFFICIENT_BALANCE_TEXT = 'Insufficient balance for quota top-up. Earn more coins from activities and achievements.';

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
    const userId = user?.Id || '';

    const [ requestType, setRequestType ] = useState<ContentRequestType>('Movie');
    const [ title, setTitle ] = useState('');
    const [ seasonNumber, setSeasonNumber ] = useState('');
    const [ rows, setRows ] = useState<ContentRequestRow[]>([]);
    const [ quota, setQuota ] = useState<ContentRequestQuotaSummary>(defaultQuota);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ isSubmitting, setIsSubmitting ] = useState(false);
    const [ formMessage, setFormMessage ] = useState('');
    const [ formError, setFormError ] = useState(false);
    const [ achievementCoinTotal, setAchievementCoinTotal ] = useState(0);
    const [ activityCoinTotal, setActivityCoinTotal ] = useState(0);
    const [ spentCoinTotal, setSpentCoinTotal ] = useState(0);

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

    const selectedRedeemCost = requestType === 'Movie' ? MOVIE_REDEEM_COINS : SERIES_REDEEM_COINS;
    const totalCoinsEarned = Math.max(0, achievementCoinTotal + activityCoinTotal);
    const availableCoins = Math.max(0, totalCoinsEarned - spentCoinTotal);
    const hasFreeQuotaForSelection = requestType === 'Movie' ? quota.remainingMovies > 0 : quota.remainingSeries > 0;
    const requiresCoinRedeemForSelection = !hasFreeQuotaForSelection;
    const hasEnoughCoinsForSelection = availableCoins >= selectedRedeemCost;
    const isCurrentSelectionAffordable = hasFreeQuotaForSelection || hasEnoughCoinsForSelection;

    const isSubmitVisible = subscriptionUiState !== 'expired';
    const isSelectionBaseEnabled = subscriptionUiState === 'active' && quota.isSubscriptionActive;
    const hasAnyRedeemableOption = availableCoins >= MOVIE_REDEEM_COINS;
    const isMovieSelectionEnabled = isSelectionBaseEnabled && (quota.remainingMovies > 0 || availableCoins >= MOVIE_REDEEM_COINS);
    const isSeriesSelectionEnabled = isSelectionBaseEnabled && (quota.remainingSeries > 0 || availableCoins >= SERIES_REDEEM_COINS);
    const isTypeSelectionEnabled = isSelectionBaseEnabled;
    const isCurrentTypeSelectionEnabled = requestType === 'Movie' ? isMovieSelectionEnabled : isSeriesSelectionEnabled;
    const shouldShowInsufficientBalance = isSelectionBaseEnabled && isQuotaExhausted && !hasAnyRedeemableOption;
    const isSubmitEnabled = isCurrentTypeSelectionEnabled;
    const requestDisclaimerPoints = [
        {
            text: globalize.translate('RequestDisclaimerPointCycle'),
            hasCoinIcon: false
        },
        {
            text: globalize.translate('RequestDisclaimerPointDuplicate'),
            hasCoinIcon: false
        },
        {
            text: globalize.translate('RequestDisclaimerPointFreeQuota'),
            hasCoinIcon: false
        },
        {
            text: globalize.translate('RequestDisclaimerPointCoinTopUp'),
            hasCoinIcon: true
        },
        {
            text: globalize.translate('RequestDisclaimerPointRedeemCost'),
            hasCoinIcon: true
        },
        {
            text: globalize.translate('RequestDisclaimerPointDeductionRule'),
            hasCoinIcon: true
        },
        {
            text: globalize.translate('RequestDisclaimerPointInsufficient'),
            hasCoinIcon: true
        }
    ];

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

    React.useEffect(() => {
        if (!userId) {
            setAchievementCoinTotal(0);
            return () => undefined;
        }

        let isCancelled = false;
        const loadAchievementCoins = async () => {
            try {
                const rows = await getAchievementHistoryApi(userId, 400);
                if (isCancelled) {
                    return;
                }

                const totalCoins = rows.reduce((total, row) => total + (Number(row.coins) || 0), 0);
                setAchievementCoinTotal(totalCoins);
            } catch (error) {
                console.warn('[RequestPage] failed to load achievement coin totals', error);
            }
        };

        void loadAchievementCoins();

        return () => {
            isCancelled = true;
        };
    }, [ userId ]);

    React.useEffect(() => {
        setActivityCoinTotal(getActivityRewardTotals(userId).coins);
        return subscribeActivityRewardHistory(() => {
            setActivityCoinTotal(getActivityRewardTotals(userId).coins);
        });
    }, [ userId ]);

    React.useEffect(() => {
        setSpentCoinTotal(getSpentCoins(userId));
    }, [ userId ]);

    React.useEffect(() => {
        if (!isSeriesSelectionEnabled && requestType === 'Series' && isMovieSelectionEnabled) {
            setRequestType('Movie');
            return;
        }

        if (!isMovieSelectionEnabled && requestType === 'Movie' && isSeriesSelectionEnabled) {
            setRequestType('Series');
        }
    }, [ isMovieSelectionEnabled, isSeriesSelectionEnabled, requestType ]);

    const onSubmit = useCallback(async (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        if (isSubmitting || !isSubmitEnabled) {
            if (isSelectionBaseEnabled && !isCurrentTypeSelectionEnabled && !hasEnoughCoinsForSelection) {
                setFormError(true);
                setFormMessage(`Need ${Math.max(0, selectedRedeemCost - availableCoins)} more coins to redeem this ${requestType.toLowerCase()}.`);
            }

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
            try {
                await syncAchievementsAndShow();
            } catch {
                // Achievement sync should not block request creation UX.
            }

            if (userId && requiresCoinRedeemForSelection) {
                const updatedSpentCoins = addSpentCoins(userId, selectedRedeemCost);
                setSpentCoinTotal(updatedSpentCoins);
            }

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
    }, [
        availableCoins,
        hasEnoughCoinsForSelection,
        isCurrentTypeSelectionEnabled,
        isSelectionBaseEnabled,
        isSubmitEnabled,
        isSubmitting,
        loadData,
        requiresCoinRedeemForSelection,
        requestType,
        seasonNumber,
        selectedRedeemCost,
        title,
        userId
    ]);

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
                    <details>
                        <summary>{globalize.translate('RequestDisclaimerTitle')}</summary>
                        <p>{globalize.translate('RequestDisclaimerBody')}</p>
                        <ul className='requestDisclaimerList'>
                            {requestDisclaimerPoints.map((point, index) => (
                                <li key={`request-disclaimer-point-${index}`}>
                                    <span className='requestDisclaimerPointContent'>
                                        {point.hasCoinIcon && (
                                            <span className='requestDisclaimerCoinInline' aria-hidden='true'>
                                                <Coin className='requestDisclaimerCoinIcon' />
                                            </span>
                                        )}
                                        <span>{point.text}</span>
                                    </span>
                                </li>
                            ))}
                        </ul>
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
                        <details className='requestCoinWallet'>
                            <summary>
                                <span className='requestCoinWalletChevron' aria-hidden='true' />
                                <span className='requestCoinWalletSummaryIcon' aria-hidden='true'>
                                    <Coin className='requestCoinWalletSummaryCoin' />
                                </span>
                                <span className='requestCoinWalletSummaryText'>
                                    Total Coins earned: {totalCoinsEarned.toLocaleString()}
                                </span>
                            </summary>
                            <div className='requestCoinWalletBody'>
                                {shouldShowInsufficientBalance && (
                                    <div className='requestCoinWalletAlert'>
                                        {INSUFFICIENT_BALANCE_TEXT}
                                    </div>
                                )}
                                <div className='requestCoinWalletRow'>
                                    <span>Available Balance</span>
                                    <strong>{availableCoins.toLocaleString()} Coins</strong>
                                </div>
                                <div className='requestCoinWalletRow'>
                                    <span>Lifetime Earnings</span>
                                    <strong>{totalCoinsEarned.toLocaleString()} Coins</strong>
                                </div>
                                <div className='requestCoinWalletRow'>
                                    <span>Total Spent</span>
                                    <strong>{spentCoinTotal.toLocaleString()} Coins</strong>
                                </div>
                                <div className='requestCoinWalletRow'>
                                    <span>Redeem Cost (Movie)</span>
                                    <strong>{MOVIE_REDEEM_COINS} Coins</strong>
                                </div>
                                <div className='requestCoinWalletRow'>
                                    <span>Redeem Cost (Series)</span>
                                    <strong>{SERIES_REDEEM_COINS} Coins</strong>
                                </div>
                                <div className={`requestCoinWalletRow requestCoinWalletRow-current${isCurrentSelectionAffordable ? ' is-ok' : ' is-low'}`}>
                                    <span>Current Selection ({requestType})</span>
                                    <strong>{hasFreeQuotaForSelection ? 'Uses monthly quota' : `${selectedRedeemCost} Coins`}</strong>
                                </div>
                                <div className={`requestCoinWalletRow${requiresCoinRedeemForSelection ? (hasEnoughCoinsForSelection ? ' requestCoinWalletRow-current is-ok' : ' requestCoinWalletRow-current is-low') : ''}`}>
                                    <span>Redeem Requirement</span>
                                    <strong>{requiresCoinRedeemForSelection ? 'Coin top-up required' : 'No redeem needed'}</strong>
                                </div>
                            </div>
                        </details>
                        <RequestForm
                            requestType={requestType}
                            title={title}
                            seasonNumber={seasonNumber}
                            isSubmitting={isSubmitting}
                            isSubmitEnabled={isSubmitEnabled}
                            isTypeSelectionEnabled={isTypeSelectionEnabled}
                            isMovieSelectionEnabled={isMovieSelectionEnabled}
                            isSeriesSelectionEnabled={isSeriesSelectionEnabled}
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
