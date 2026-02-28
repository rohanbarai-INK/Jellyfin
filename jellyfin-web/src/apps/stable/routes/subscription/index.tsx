import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import FormControlLabel from '@mui/material/FormControlLabel';
import LinearProgress from '@mui/material/LinearProgress';
import Stack from '@mui/material/Stack';
import Switch from '@mui/material/Switch';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import { alpha, useTheme } from '@mui/material/styles';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import { useNamedConfiguration } from 'hooks/useNamedConfiguration';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import globalize from 'lib/globalize';
import Dashboard from 'utils/dashboard';
import events from 'utils/events';
import {
    isExpiredSubscriptionUser,
    isInGraceSubscriptionUser,
    normalizeSubscriptionPricing,
    SUBSCRIPTION_CONFIG_KEY,
    SubscriptionPricingConfig,
    SubscriptionPricing
} from 'utils/subscription';

type PlanDuration = 1 | 3 | 6 | 12;

interface SubscriptionPlan {
    durationMonths: PlanDuration
    title: string
    description: string
    isPopular?: boolean
}

type CurrentSubscriptionMetadata = {
    ExpiryDate?: string | null
    expiryDate?: string | null
    Status?: string | null
    status?: string | null
    IsInGracePeriod?: boolean | null
    isInGracePeriod?: boolean | null
    GraceDaysRemaining?: number | null
    graceDaysRemaining?: number | null
    LastDurationMonths?: number | null
    lastDurationMonths?: number | null
    LastRedeemedAt?: string | null
    lastRedeemedAt?: string | null
};

const SUBSCRIPTION_PLANS: SubscriptionPlan[] = [
    {
        durationMonths: 1,
        title: 'Starter',
        description: 'Short-term access'
    },
    {
        durationMonths: 3,
        title: 'Standard',
        description: 'Balanced option'
    },
    {
        durationMonths: 6,
        title: 'Pro',
        description: 'Best overall value',
        isPopular: true
    },
    {
        durationMonths: 12,
        title: 'Annual',
        description: 'Longest uninterrupted access'
    }
];

const DAY_IN_MS = 24 * 60 * 60 * 1000;
const AUTO_RENEW_STORAGE_KEY = 'jf.subscription.autoRenew.visual';
const PLAN_DURATION_DAY_LOOKUP: Record<PlanDuration, number> = {
    1: 30,
    3: 90,
    6: 180,
    12: 365
};
const DESKTOP_HOVER_MEDIA_QUERY = '@media (hover: hover) and (pointer: fine)';

const getStatusErrorMessage = (statusCode: number | undefined) => {
    if (statusCode === 401) {
        return 'Unauthorized request. Sign out and sign in again, then retry.';
    }

    if (statusCode === 404) {
        return 'Redeem endpoint not found. Make sure the backend has access key support.';
    }

    return undefined;
};

const getResponseErrorMessage = (responseData: unknown): string | undefined => {
    if (typeof responseData === 'string' && responseData.trim()) {
        return responseData;
    }

    if (!responseData || typeof responseData !== 'object') {
        return undefined;
    }

    const payload = responseData as Record<string, unknown>;
    const message = payload.message ?? payload.Message ?? payload.error ?? payload.Error;
    return typeof message === 'string' && message.trim() ? message : undefined;
};

const getServerErrorMessage = async (err: unknown): Promise<string | undefined> => {
    if (err instanceof Response) {
        const statusMessage = getStatusErrorMessage(err.status);
        if (statusMessage) {
            return statusMessage;
        }

        const responseText = await err.text();
        return responseText.trim() || undefined;
    }

    const error = err as {
        status?: number;
        text?: () => Promise<string>;
        response?: { status?: number; data?: unknown };
    };

    const statusCode = error.response?.status ?? error.status;
    const responseData = error.response?.data;
    const statusMessage = getStatusErrorMessage(statusCode);
    if (statusMessage) {
        return statusMessage;
    }

    if (typeof error.text === 'function') {
        const responseText = await error.text();
        if (responseText.trim()) {
            return responseText;
        }
    }

    return getResponseErrorMessage(responseData);
};

const getPlanPrice = (pricing: SubscriptionPricing, durationMonths: PlanDuration) => {
    switch (durationMonths) {
        case 1:
            return pricing.OneMonthPrice;
        case 3:
            return pricing.ThreeMonthPrice;
        case 6:
            return pricing.SixMonthPrice;
        default:
            return pricing.TwelveMonthPrice;
    }
};

const formatPrice = (value: number) => (
    Number.isInteger(value) ? value.toString() : value.toFixed(2)
);

const getInitialAutoRenewToggleState = () => {
    try {
        return window.localStorage.getItem(AUTO_RENEW_STORAGE_KEY) === 'true';
    } catch (_err) {
        return false;
    }
};

export const Component = () => {
    const { user } = useApi();
    const theme = useTheme();
    const [ accessKey, setAccessKey ] = useState('');
    const [ isRedeemingKey, setIsRedeemingKey ] = useState(false);
    const [ redeemErrorMessage, setRedeemErrorMessage ] = useState('');
    const [ redeemSuccessMessage, setRedeemSuccessMessage ] = useState('');
    const [ currentSubscription, setCurrentSubscription ] = useState<CurrentSubscriptionMetadata | null>(null);
    const [ currentSubscriptionError, setCurrentSubscriptionError ] = useState('');
    const [ isLoadingCurrentSubscription, setIsLoadingCurrentSubscription ] = useState(false);
    const [ autoRenewEnabled, setAutoRenewEnabled ] = useState(getInitialAutoRenewToggleState);
    const plansSectionRef = useRef<HTMLDivElement | null>(null);

    const {
        data: pricingConfig,
        isPending,
        isError
    } = useNamedConfiguration<SubscriptionPricingConfig>(SUBSCRIPTION_CONFIG_KEY);

    const pricing = useMemo(
        () => normalizeSubscriptionPricing(pricingConfig),
        [ pricingConfig ]);

    const isExpiredUser = useMemo(
        () => isExpiredSubscriptionUser(user),
        [ user ]);

    const isInGraceUser = useMemo(
        () => isInGraceSubscriptionUser(user),
        [ user ]);

    const lastDurationMonths = useMemo(() => {
        const value = Number(currentSubscription?.LastDurationMonths ?? currentSubscription?.lastDurationMonths);
        return Number.isInteger(value) && [ 1, 3, 6, 12 ].includes(value)
            ? value as PlanDuration
            : null;
    }, [ currentSubscription ]);

    const currentPlanTitle = useMemo(() => {
        if (!lastDurationMonths) {
            return 'Unknown';
        }

        return SUBSCRIPTION_PLANS.find(plan => plan.durationMonths === lastDurationMonths)?.title || 'Unknown';
    }, [ lastDurationMonths ]);

    const subscriptionStatus = useMemo(() => {
        const status = currentSubscription?.Status ?? currentSubscription?.status;
        if (typeof status === 'string' && status.trim()) {
            return status;
        }

        if (isInGraceUser) {
            return 'Grace';
        }

        return isExpiredUser ? 'Expired' : 'Active';
    }, [ currentSubscription, isExpiredUser, isInGraceUser ]);

    const userExpiryDate = useMemo(() => {
        const candidateUser = user as {
            ExpiryDate?: string | null
            expiryDate?: string | null
        } | null | undefined;

        return candidateUser?.ExpiryDate ?? candidateUser?.expiryDate ?? null;
    }, [ user ]);

    const userGraceDaysRemaining = useMemo(() => {
        const candidateUser = user as {
            GraceDaysRemaining?: number | null
            graceDaysRemaining?: number | null
        } | null | undefined;
        const parsedValue = Number(candidateUser?.GraceDaysRemaining ?? candidateUser?.graceDaysRemaining);
        return Number.isFinite(parsedValue) && parsedValue >= 0 ? Math.trunc(parsedValue) : 0;
    }, [ user ]);

    const subscriptionExpiryDate = useMemo(() => {
        const expiryRaw = currentSubscription?.ExpiryDate
            ?? currentSubscription?.expiryDate
            ?? userExpiryDate
            ?? null;

        if (!expiryRaw) {
            return null;
        }

        const parsedDate = new Date(expiryRaw);
        return Number.isNaN(parsedDate.getTime()) ? null : parsedDate;
    }, [ currentSubscription, userExpiryDate ]);

    const isInGracePeriod = useMemo(() => {
        const graceFlag = currentSubscription?.IsInGracePeriod ?? currentSubscription?.isInGracePeriod;
        if (typeof graceFlag === 'boolean') {
            return graceFlag;
        }

        return isInGraceUser;
    }, [ currentSubscription, isInGraceUser ]);

    const graceDaysRemaining = useMemo(() => {
        const value = Number(
            currentSubscription?.GraceDaysRemaining
            ?? currentSubscription?.graceDaysRemaining
            ?? userGraceDaysRemaining);
        return Number.isFinite(value) && value >= 0 ? Math.trunc(value) : 0;
    }, [ currentSubscription, userGraceDaysRemaining ]);

    const graceDaysTotal = pricing.GracePeriodDays;

    const graceDaysElapsed = useMemo(() => {
        if (!isInGracePeriod || !subscriptionExpiryDate) {
            return 0;
        }

        return Math.max(0, Math.ceil((Date.now() - subscriptionExpiryDate.getTime()) / DAY_IN_MS));
    }, [ isInGracePeriod, subscriptionExpiryDate ]);

    const validUntilText = useMemo(() => {
        if (!subscriptionExpiryDate) {
            return 'Not set';
        }

        return subscriptionExpiryDate.toLocaleDateString(undefined, {
            day: '2-digit',
            month: 'short',
            year: 'numeric'
        });
    }, [ subscriptionExpiryDate ]);

    const daysRemaining = useMemo(() => {
        if (!subscriptionExpiryDate) {
            return 0;
        }

        return Math.max(0, Math.ceil((subscriptionExpiryDate.getTime() - Date.now()) / DAY_IN_MS));
    }, [ subscriptionExpiryDate ]);

    const totalPlanDays = useMemo(
        () => (lastDurationMonths ? PLAN_DURATION_DAY_LOOKUP[lastDurationMonths] : 0),
        [ lastDurationMonths ]);

    const progressPercent = useMemo(() => {
        if (!totalPlanDays) {
            return 0;
        }

        return Math.max(0, Math.min(100, (daysRemaining / totalPlanDays) * 100));
    }, [ daysRemaining, totalPlanDays ]);

    const graceProgressPercent = useMemo(() => {
        if (!isInGracePeriod || graceDaysTotal <= 0) {
            return 0;
        }

        return Math.max(0, Math.min(100, (graceDaysRemaining / graceDaysTotal) * 100));
    }, [ graceDaysRemaining, graceDaysTotal, isInGracePeriod ]);

    const progressColor = useMemo(() => {
        if (isInGracePeriod) {
            return '#ff9800';
        }

        if (daysRemaining > 30) {
            return '#2ecc71';
        }

        if (daysRemaining >= 7) {
            return '#ff9800';
        }

        return '#ff5252';
    }, [ daysRemaining, isInGracePeriod ]);

    useEffect(() => {
        try {
            window.localStorage.setItem(AUTO_RENEW_STORAGE_KEY, autoRenewEnabled ? 'true' : 'false');
        } catch (_err) {
            // Ignore storage errors in embedded browsers.
        }
    }, [ autoRenewEnabled ]);

    useEffect(() => {
        if (isExpiredUser || !user?.Id) {
            setCurrentSubscription(null);
            setCurrentSubscriptionError('');
            setIsLoadingCurrentSubscription(false);
            return;
        }

        const apiClient = ServerConnections.currentApiClient();
        if (!apiClient) {
            setCurrentSubscriptionError('Unable to load active subscription metadata.');
            return;
        }

        let isDisposed = false;
        setIsLoadingCurrentSubscription(true);
        setCurrentSubscriptionError('');

        apiClient.ajax({
            type: 'GET',
            url: apiClient.getUrl('Keys/CurrentSubscription'),
            dataType: 'json',
            contentType: 'application/json'
        }).then((response: unknown) => {
            if (!isDisposed) {
                setCurrentSubscription(response as CurrentSubscriptionMetadata);
            }
        }).catch(async err => {
            console.error('[subscription] failed to load current subscription metadata', err);
            if (!isDisposed) {
                setCurrentSubscription(null);
                setCurrentSubscriptionError((await getServerErrorMessage(err)) || 'Unable to load active subscription metadata.');
            }
        }).finally(() => {
            if (!isDisposed) {
                setIsLoadingCurrentSubscription(false);
            }
        });

        return () => {
            isDisposed = true;
        };
    }, [ isExpiredUser, user?.Id ]);

    const onAccessKeyChange = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
        setAccessKey(event.target.value);
    }, []);

    const onRedeemAccessKey = useCallback(async () => {
        if (!accessKey.trim()) {
            return;
        }

        const apiClient = ServerConnections.currentApiClient();
        if (!apiClient) {
            setRedeemErrorMessage('Unable to reach server API.');
            return;
        }

        setRedeemErrorMessage('');
        setRedeemSuccessMessage('');
        setIsRedeemingKey(true);
        try {
            await apiClient.ajax({
                type: 'POST',
                url: apiClient.getUrl('Keys/Redeem'),
                data: JSON.stringify({
                    Key: accessKey.trim()
                }),
                dataType: 'json',
                contentType: 'application/json'
            });

            const currentUserId = apiClient.getCurrentUserId?.() || user?.Id;
            const refreshedUser = currentUserId
                ? await apiClient.getUser(currentUserId)
                : await apiClient.getCurrentUser();

            // Keep legacy apiClient user cache aligned with the refreshed server user.
            (apiClient as { _currentUser?: unknown })._currentUser = refreshedUser;
            const refreshedServerId = refreshedUser?.ServerId || apiClient.serverId?.();
            if (refreshedUser?.Id && refreshedServerId) {
                try {
                    window.localStorage.setItem(`user-${refreshedUser.Id}-${refreshedServerId}`, JSON.stringify(refreshedUser));
                } catch (_storageError) {
                    // Ignore storage write failures in embedded browsers.
                }
            }
            events.trigger(ServerConnections, 'localusersignedin', [ refreshedUser ]);

            setAccessKey('');
            setRedeemSuccessMessage('Redeem successful. Subscription activated. All features are now available.');
        } catch (err) {
            console.error('[subscription] failed to redeem access key', err);
            setRedeemErrorMessage((await getServerErrorMessage(err)) || 'Failed to redeem access key. Check the key and try again.');
        } finally {
            setIsRedeemingKey(false);
        }
    }, [ accessKey, user?.Id ]);

    const onLogoutClick = useCallback(() => {
        Dashboard.logout();
    }, []);

    const onPlanCardClick = useCallback(() => {
        // Intentionally empty until payment flow is implemented.
    }, []);

    const onRenewNowClick = useCallback(() => {
        plansSectionRef.current?.scrollIntoView({
            behavior: 'smooth',
            block: 'start'
        });
    }, []);

    if (isPending) {
        return <Loading />;
    }

    return (
        <Page
            id='subscriptionPage'
            className='mainAnimatedPage noSecondaryNavPage'
            title='Subscription'
            shouldAutoFocus
        >
            <Box
                sx={{
                    minHeight: '100%',
                    background: 'linear-gradient(145deg, rgba(25, 48, 79, 0.35) 0%, rgba(10, 18, 30, 0.85) 65%, rgba(7, 11, 18, 0.95) 100%)',
                    pt: {
                        xs: 10,
                        md: 6
                    },
                    pb: {
                        xs: 4,
                        md: 6
                    },
                    px: {
                        xs: 2,
                        md: 4
                    }
                }}
            >
                <Box
                    sx={{
                        maxWidth: 1100,
                        margin: '0 auto'
                    }}
                >
                    <Stack spacing={4}>
                        <Stack
                            spacing={1}
                            sx={{
                                pr: {
                                    xs: 8,
                                    sm: 0
                                }
                            }}
                        >
                            <Typography variant='h3' sx={{ fontWeight: 700 }}>
                                {isExpiredUser ? 'Subscription Required' : 'Subscription Management'}
                            </Typography>
                            <Typography sx={{ opacity: 0.8 }}>
                                {isExpiredUser
                                    ? 'Your account access is limited until a valid key is redeemed.'
                                    : 'Manage your active plan and renew before it expires.'}
                            </Typography>
                            {!!user?.Name && (
                                <Typography sx={{ opacity: 0.75 }}>
                                    Signed in as {user.Name}
                                </Typography>
                            )}
                        </Stack>

                        {isInGracePeriod && (
                            <Alert severity='warning'>
                                {`Your subscription expired ${graceDaysElapsed} day${graceDaysElapsed === 1 ? '' : 's'} ago. `}
                                {`You are in a ${graceDaysTotal}-day grace period with ${graceDaysRemaining} day${graceDaysRemaining === 1 ? '' : 's'} remaining. `}
                                Renew now to continue uninterrupted.
                            </Alert>
                        )}

                        {!isExpiredUser && (
                            <Card
                                sx={{
                                    borderRadius: 3,
                                    border: '1px solid rgba(255, 255, 255, 0.12)',
                                    background: 'rgba(13, 18, 30, 0.88)'
                                }}
                            >
                                <CardContent sx={{ p: 3 }}>
                                    <Stack spacing={2.2}>
                                        <Stack
                                            direction={{
                                                xs: 'column',
                                                sm: 'row'
                                            }}
                                            spacing={2}
                                            justifyContent='space-between'
                                            alignItems={{
                                                xs: 'flex-start',
                                                sm: 'center'
                                            }}
                                        >
                                            <Stack spacing={0.8}>
                                                <Typography variant='h6' sx={{ fontWeight: 700 }}>
                                                    Current Plan
                                                </Typography>
                                                <Box
                                                    component='span'
                                                    sx={{
                                                        alignSelf: 'flex-start',
                                                        px: 1.5,
                                                        py: 0.6,
                                                        borderRadius: 999,
                                                        fontSize: 12,
                                                        fontWeight: 700,
                                                        color: '#dff8ff',
                                                        background: 'linear-gradient(90deg, rgba(37, 169, 255, 0.35), rgba(49, 107, 238, 0.38))',
                                                        border: '1px solid rgba(125, 204, 255, 0.5)',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    {currentPlanTitle}
                                                </Box>
                                            </Stack>

                                            <FormControlLabel
                                                control={(
                                                    <Switch
                                                        checked={autoRenewEnabled}
                                                        onChange={(event) => {
                                                            setAutoRenewEnabled(event.target.checked);
                                                        }}
                                                        color='primary'
                                                    />
                                                )}
                                                label={autoRenewEnabled ? 'Auto-Renew On' : 'Auto-Renew Off'}
                                            />
                                        </Stack>

                                        {isLoadingCurrentSubscription && (
                                            <Typography sx={{ opacity: 0.75 }}>
                                                Loading current subscription details...
                                            </Typography>
                                        )}

                                        {!!currentSubscriptionError && (
                                            <Alert severity='warning'>
                                                {currentSubscriptionError}
                                            </Alert>
                                        )}

                                        <Stack spacing={0.6}>
                                            <Typography sx={{ opacity: 0.8 }}>
                                                Status: {subscriptionStatus}
                                            </Typography>
                                            <Typography sx={{ opacity: 0.8 }}>
                                                Valid until: {validUntilText}
                                            </Typography>
                                            <Typography sx={{ color: progressColor, fontWeight: 600 }}>
                                                {isInGracePeriod
                                                    ? `${graceDaysRemaining} grace day${graceDaysRemaining === 1 ? '' : 's'} remaining`
                                                    : `${daysRemaining} day${daysRemaining === 1 ? '' : 's'} remaining`}
                                            </Typography>
                                        </Stack>

                                        <Stack spacing={0.8}>
                                            <LinearProgress
                                                variant='determinate'
                                                value={isInGracePeriod ? graceProgressPercent : progressPercent}
                                                sx={{
                                                    height: 10,
                                                    borderRadius: 999,
                                                    backgroundColor: 'rgba(255, 255, 255, 0.12)',
                                                    '& .MuiLinearProgress-bar': {
                                                        borderRadius: 999,
                                                        backgroundColor: progressColor,
                                                        transition: 'transform 600ms ease'
                                                    }
                                                }}
                                            />
                                            <Typography sx={{ fontSize: 12, opacity: 0.7 }}>
                                                {isInGracePeriod
                                                    ? `${Math.round(graceProgressPercent)}% of grace period remaining`
                                                    : `${Math.round(progressPercent)}% of current cycle remaining`}
                                            </Typography>
                                        </Stack>

                                        {daysRemaining < 7 && (
                                            <Button
                                                onClick={onRenewNowClick}
                                                variant='contained'
                                                sx={{
                                                    alignSelf: 'flex-start',
                                                    fontWeight: 700,
                                                    px: 2.2,
                                                    py: 0.9
                                                }}
                                            >
                                                Renew Now
                                            </Button>
                                        )}
                                    </Stack>
                                </CardContent>
                            </Card>
                        )}

                        {isError && (
                            <Alert severity='error'>
                                Unable to load subscription pricing. Showing default values.
                            </Alert>
                        )}

                        <Box
                            ref={plansSectionRef}
                            sx={{
                                display: 'grid',
                                gridTemplateColumns: {
                                    xs: '1fr',
                                    sm: 'repeat(2, minmax(0, 1fr))',
                                    lg: 'repeat(4, minmax(0, 1fr))'
                                },
                                gap: 2,
                                alignItems: 'stretch'
                            }}
                        >
                            {SUBSCRIPTION_PLANS.map(plan => {
                                const actualPrice = getPlanPrice(pricing, plan.durationMonths);
                                const originalPrice = pricing.BasePricePerMonth * plan.durationMonths;
                                const savingsAmount = originalPrice - actualPrice;
                                const hasSavings = savingsAmount > 0;
                                const savingsPercent = hasSavings && originalPrice > 0 ? Math.trunc((savingsAmount / originalPrice) * 100) : 0;
                                const isLastPlan = !isExpiredUser && lastDurationMonths === plan.durationMonths;
                                const paperColor = theme.palette.background.paper;
                                const defaultBackgroundColor = theme.palette.background.default;
                                const primaryAccentColor = theme.palette.primary.main;
                                const successAccentColor = theme.palette.success.main;
                                const surfaceLightColor = theme.palette.common.white;
                                const surfaceDarkColor = theme.palette.common.black;

                                let cardBorder = `1px solid ${alpha(surfaceLightColor, 0.18)}`;
                                let cardBackground = `linear-gradient(160deg, ${alpha(paperColor, 0.86)} 0%, ${alpha(defaultBackgroundColor, 0.96)} 100%)`;
                                let cardShadow = `0 10px 22px ${alpha(surfaceDarkColor, 0.36)}`;
                                let hoverBackground = `linear-gradient(160deg, ${alpha(paperColor, 0.95)} 0%, ${alpha(defaultBackgroundColor, 0.99)} 100%)`;
                                let planAccentColor = primaryAccentColor;
                                let ambientGlowColor = alpha(primaryAccentColor, 0.34);
                                let sweepStrongColor = alpha(primaryAccentColor, 0.42);
                                let sweepSoftColor = alpha(primaryAccentColor, 0.2);

                                if (plan.isPopular) {
                                    cardBorder = `1px solid ${alpha(primaryAccentColor, 0.75)}`;
                                    cardBackground = `linear-gradient(160deg, ${alpha(primaryAccentColor, 0.26)} 0%, ${alpha(defaultBackgroundColor, 0.98)} 100%)`;
                                    cardShadow = `0 14px 30px ${alpha(surfaceDarkColor, 0.46)}`;
                                    hoverBackground = `linear-gradient(160deg, ${alpha(primaryAccentColor, 0.35)} 0%, ${alpha(defaultBackgroundColor, 0.99)} 100%)`;
                                    ambientGlowColor = alpha(primaryAccentColor, 0.4);
                                    sweepStrongColor = alpha(primaryAccentColor, 0.5);
                                    sweepSoftColor = alpha(primaryAccentColor, 0.26);
                                }

                                if (isLastPlan) {
                                    planAccentColor = successAccentColor;
                                    cardBorder = `1px solid ${alpha(successAccentColor, 0.82)}`;
                                    cardShadow = `0 14px 30px ${alpha(surfaceDarkColor, 0.45)}, 0 0 0 1px ${alpha(successAccentColor, 0.32)}`;
                                    ambientGlowColor = alpha(successAccentColor, 0.42);
                                    sweepStrongColor = alpha(successAccentColor, 0.52);
                                    sweepSoftColor = alpha(successAccentColor, 0.3);
                                }

                                const highlightedCardStyles = {
                                    borderColor: alpha(planAccentColor, 0.95),
                                    background: hoverBackground,
                                    boxShadow: `0 0 0 1px ${alpha(planAccentColor, 0.36)}, 0 20px 38px ${alpha(surfaceDarkColor, 0.5)}, 0 0 46px ${ambientGlowColor}`,
                                    transform: 'translateY(-4px) scale(1.015)'
                                };

                                return (
                                    <Box
                                        key={plan.durationMonths}
                                        sx={{
                                            '@keyframes subscriptionPlanInnerSweep': {
                                                '0%': {
                                                    opacity: 0,
                                                    transform: 'rotate(-45deg) translateY(-120%)'
                                                },
                                                '18%': {
                                                    opacity: 0.9
                                                },
                                                '55%': {
                                                    opacity: 1
                                                },
                                                '100%': {
                                                    opacity: 0,
                                                    transform: 'rotate(-45deg) translateY(120%)'
                                                }
                                            },
                                            position: 'relative',
                                            overflow: 'visible',
                                            isolation: 'isolate',
                                            display: 'flex',
                                            flexDirection: 'column',
                                            height: '100%',
                                            transition: 'transform 300ms ease',
                                            '&::after': {
                                                content: '""',
                                                position: 'absolute',
                                                inset: '-8px -6px -14px -6px',
                                                borderRadius: 24,
                                                background: `radial-gradient(65% 80% at 50% 50%, ${ambientGlowColor} 0%, ${alpha(planAccentColor, 0.14)} 48%, ${alpha(planAccentColor, 0)} 100%)`,
                                                filter: 'blur(20px)',
                                                opacity: 0.18,
                                                transform: 'scale(0.96)',
                                                transition: 'opacity 320ms ease, transform 320ms ease',
                                                pointerEvents: 'none',
                                                zIndex: 0
                                            },
                                            [DESKTOP_HOVER_MEDIA_QUERY]: {
                                                '&:hover': {
                                                    transform: 'translateY(-6px) scale(1.022)'
                                                },
                                                '&:hover::after': {
                                                    opacity: 0.88,
                                                    transform: 'scale(1.04)'
                                                },
                                                '&:hover .subscriptionPlanCard': highlightedCardStyles,
                                                '&:hover .subscriptionPopularBadgeWrap': {
                                                    transform: 'translate(-50%, -62%) scale(1.05)'
                                                },
                                                '&:hover .subscriptionPopularBadge': {
                                                    boxShadow: `0 14px 30px ${alpha(surfaceDarkColor, 0.42)}`
                                                },
                                                '&:hover .subscriptionPlanCard::before': {
                                                    opacity: 1,
                                                    animation: 'subscriptionPlanInnerSweep 1.05s cubic-bezier(0.22, 1, 0.36, 1) infinite',
                                                    transform: 'rotate(-45deg) translateY(120%)'
                                                }
                                            },
                                            '@media (hover: none), (pointer: coarse)': {
                                                '&:active .subscriptionPlanCard': highlightedCardStyles
                                            },
                                            '@media (prefers-reduced-motion: reduce)': {
                                                transition: 'none',
                                                '&:hover': {
                                                    transform: 'none'
                                                },
                                                '&::after': {
                                                    transition: 'none'
                                                },
                                                '&:hover::after': {
                                                    opacity: 0.18,
                                                    transform: 'scale(0.96)'
                                                },
                                                '& .subscriptionPlanCard': {
                                                    transition: 'none',
                                                    transform: 'none'
                                                },
                                                '& .subscriptionPopularBadgeWrap': {
                                                    transition: 'none'
                                                },
                                                '& .subscriptionPopularBadge': {
                                                    transition: 'none'
                                                },
                                                '& .subscriptionPlanCard::before': {
                                                    transition: 'none',
                                                    opacity: 0,
                                                    animation: 'none'
                                                },
                                                '&:hover .subscriptionPlanCard::before': {
                                                    transform: 'rotate(-45deg) translateY(-120%)'
                                                }
                                            }
                                        }}
                                    >
                                        {plan.isPopular && (
                                            <Box
                                                className='subscriptionPopularBadgeWrap'
                                                sx={{
                                                    position: 'absolute',
                                                    top: 0,
                                                    left: '50%',
                                                    transform: 'translate(-50%, -50%)',
                                                    transition: 'transform 320ms ease',
                                                    zIndex: 10,
                                                    pointerEvents: 'none'
                                                }}
                                            >
                                                <Box
                                                    component='span'
                                                    className='subscriptionPopularBadge'
                                                    sx={{
                                                        display: 'inline-block',
                                                        px: 1.5,
                                                        py: 0.45,
                                                        borderRadius: 999,
                                                        fontSize: 12,
                                                        fontWeight: 600,
                                                        lineHeight: 1.2,
                                                        whiteSpace: 'nowrap',
                                                        color: '#fff',
                                                        background: 'linear-gradient(90deg, #ff9800 0%, #ff5722 100%)',
                                                        boxShadow: '0 10px 24px rgba(0, 0, 0, 0.3)',
                                                        transition: 'box-shadow 320ms ease'
                                                    }}
                                                >
                                                    Most Popular
                                                </Box>
                                            </Box>
                                        )}
                                        <Card
                                            className='subscriptionPlanCard'
                                            onClick={onPlanCardClick}
                                            sx={{
                                                position: 'relative',
                                                overflow: 'hidden',
                                                display: 'flex',
                                                flexDirection: 'column',
                                                flexGrow: 1,
                                                height: '100%',
                                                zIndex: 1,
                                                borderRadius: 3,
                                                border: cardBorder,
                                                background: cardBackground,
                                                boxShadow: cardShadow,
                                                cursor: 'pointer',
                                                transform: 'translateY(0) scale(1)',
                                                transformOrigin: 'center center',
                                                transition: 'box-shadow 320ms ease, border-color 320ms ease, background 320ms ease, transform 320ms ease',
                                                '&::before': {
                                                    content: '""',
                                                    position: 'absolute',
                                                    top: '-50%',
                                                    left: '-50%',
                                                    width: '200%',
                                                    height: '200%',
                                                    background: `linear-gradient(
                                                        0deg,
                                                        transparent 0%,
                                                        transparent 34%,
                                                        ${alpha(surfaceLightColor, 0.06)} 46%,
                                                        ${sweepStrongColor} 60%,
                                                        ${alpha(surfaceLightColor, 0.28)} 68%,
                                                        ${sweepSoftColor} 78%,
                                                        transparent 100%
                                                    )`,
                                                    transform: 'rotate(-45deg) translateY(-120%)',
                                                    opacity: 0,
                                                    transition: 'opacity 180ms ease',
                                                    animation: 'none',
                                                    mixBlendMode: 'screen',
                                                    willChange: 'transform, opacity',
                                                    pointerEvents: 'none',
                                                    zIndex: 1
                                                }
                                            }}
                                        >
                                            {isLastPlan && (
                                                <Box
                                                    sx={{
                                                        position: 'absolute',
                                                        top: 12,
                                                        right: 12,
                                                        px: 1.2,
                                                        py: 0.35,
                                                        borderRadius: 999,
                                                        fontSize: 11,
                                                        fontWeight: 700,
                                                        color: alpha(surfaceLightColor, 0.95),
                                                        backgroundColor: alpha(successAccentColor, 0.35),
                                                        border: `1px solid ${alpha(successAccentColor, 0.6)}`,
                                                        whiteSpace: 'nowrap',
                                                        zIndex: 2
                                                    }}
                                                >
                                                    Your Last Plan
                                                </Box>
                                            )}
                                            <CardContent
                                                sx={{
                                                    p: 3,
                                                    pt: 3,
                                                    position: 'relative',
                                                    zIndex: 1,
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    flexGrow: 1
                                                }}
                                            >
                                                <Stack spacing={1.5} sx={{ flexGrow: 1 }}>
                                                    <Typography variant='h6' sx={{ fontWeight: 700 }}>
                                                        {plan.title}
                                                    </Typography>
                                                    <Typography sx={{ opacity: 0.8 }}>
                                                        {plan.description}
                                                    </Typography>
                                                    <Stack spacing={0.6} sx={{ mt: 0.5 }}>
                                                        {hasSavings && (
                                                            <Typography
                                                                sx={{
                                                                    opacity: 0.65,
                                                                    textDecoration: 'line-through'
                                                                }}
                                                            >
                                                                Rs {formatPrice(originalPrice)}
                                                            </Typography>
                                                        )}
                                                        <Typography
                                                            variant='h4'
                                                            sx={{
                                                                fontWeight: 700
                                                            }}
                                                        >
                                                            Rs {formatPrice(actualPrice)}
                                                        </Typography>
                                                        {hasSavings && (
                                                            <Box
                                                                sx={{
                                                                    alignSelf: 'flex-start',
                                                                    mt: 0.3,
                                                                    px: 1.2,
                                                                    py: 0.4,
                                                                    borderRadius: 1.5,
                                                                    fontSize: 13,
                                                                    fontWeight: 700,
                                                                    color: 'rgb(194, 240, 208)',
                                                                    backgroundColor: 'rgba(34, 102, 63, 0.35)',
                                                                    border: '1px solid rgba(96, 205, 140, 0.5)'
                                                                }}
                                                            >
                                                                Save Rs {formatPrice(savingsAmount)} ({savingsPercent}%)
                                                            </Box>
                                                        )}
                                                    </Stack>
                                                </Stack>
                                                <Typography sx={{ mt: 1.5, opacity: 0.75 }}>
                                                    {plan.durationMonths} month{plan.durationMonths === 1 ? '' : 's'}
                                                </Typography>
                                            </CardContent>
                                        </Card>
                                    </Box>
                                );
                            })}
                        </Box>

                        <Card
                            sx={{
                                borderRadius: 3,
                                border: '1px solid rgba(255, 255, 255, 0.12)',
                                background: 'rgba(13, 18, 30, 0.88)'
                            }}
                        >
                            <CardContent sx={{ p: 3 }}>
                                <Stack spacing={2}>
                                    <Typography variant='h6' sx={{ fontWeight: 700 }}>
                                        Redeem Access Key
                                    </Typography>
                                    <TextField
                                        value={accessKey}
                                        onChange={onAccessKeyChange}
                                        label='Access Key'
                                        placeholder='JF-XXXX-XXXX'
                                        fullWidth
                                    />
                                    <Stack
                                        direction={{
                                            xs: 'column',
                                            sm: 'row'
                                        }}
                                        spacing={1.5}
                                    >
                                        <Button
                                            className='jfAnimatedActionButton subscriptionRedeemButton'
                                            onClick={onRedeemAccessKey}
                                            disabled={isRedeemingKey || !accessKey.trim()}
                                            size='large'
                                        >
                                            <span className='jfAnimatedActionButtonLabel'>
                                                {isRedeemingKey ? 'Redeeming...' : 'Redeem Key'}
                                            </span>
                                        </Button>
                                        <Button
                                            variant='outlined'
                                            color='inherit'
                                            onClick={onLogoutClick}
                                            size='large'
                                        >
                                            {globalize.translate('ButtonSignOut')}
                                        </Button>
                                    </Stack>
                                    {!!redeemSuccessMessage && (
                                        <Alert severity='success'>{redeemSuccessMessage}</Alert>
                                    )}
                                    {!!redeemErrorMessage && (
                                        <Alert severity='error'>{redeemErrorMessage}</Alert>
                                    )}
                                </Stack>
                            </CardContent>
                        </Card>
                    </Stack>
                </Box>
            </Box>
        </Page>
    );
};

Component.displayName = 'SubscriptionPage';

