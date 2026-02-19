import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Stack from '@mui/material/Stack';
import TextField from '@mui/material/TextField';
import Typography from '@mui/material/Typography';
import React, { useCallback, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import { useNamedConfiguration } from 'hooks/useNamedConfiguration';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import globalize from 'lib/globalize';
import Dashboard from 'utils/dashboard';
import events from 'utils/events';
import {
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

export const Component = () => {
    const navigate = useNavigate();
    const { user } = useApi();
    const [ accessKey, setAccessKey ] = useState('');
    const [ isRedeemingKey, setIsRedeemingKey ] = useState(false);
    const [ redeemErrorMessage, setRedeemErrorMessage ] = useState('');
    const [ redeemSuccessMessage, setRedeemSuccessMessage ] = useState('');

    const {
        data: pricingConfig,
        isPending,
        isError
    } = useNamedConfiguration<SubscriptionPricingConfig>(SUBSCRIPTION_CONFIG_KEY);

    const pricing = useMemo(
        () => normalizeSubscriptionPricing(pricingConfig),
        [ pricingConfig ]);

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

            const refreshedUser = await apiClient.getCurrentUser();
            events.trigger(ServerConnections, 'localusersignedin', [ refreshedUser ]);

            setAccessKey('');
            setRedeemSuccessMessage('Access key redeemed successfully.');
            navigate('/home', { replace: true });
        } catch (err) {
            console.error('[subscription] failed to redeem access key', err);
            setRedeemErrorMessage((await getServerErrorMessage(err)) || 'Failed to redeem access key. Check the key and try again.');
        } finally {
            setIsRedeemingKey(false);
        }
    }, [ accessKey, navigate ]);

    const onLogoutClick = useCallback(() => {
        Dashboard.logout();
    }, []);

    const onPlanCardClick = useCallback(() => {
        // Intentionally empty until payment flow is implemented.
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
                                Subscription Required
                            </Typography>
                            <Typography sx={{ opacity: 0.8 }}>
                                Your account access is limited until a valid key is redeemed.
                            </Typography>
                            {!!user?.Name && (
                                <Typography sx={{ opacity: 0.75 }}>
                                    Signed in as {user.Name}
                                </Typography>
                            )}
                        </Stack>

                        {isError && (
                            <Alert severity='error'>
                                Unable to load subscription pricing. Showing default values.
                            </Alert>
                        )}

                        <Box
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

                                let cardBorder = '1px solid rgba(255, 255, 255, 0.12)';
                                let cardBackground = 'linear-gradient(160deg, rgba(17, 24, 37, 0.86) 0%, rgba(9, 14, 25, 0.95) 100%)';
                                let cardShadow = '0 10px 22px rgba(4, 8, 16, 0.3)';
                                let hoverBackground = 'linear-gradient(160deg, rgba(28, 40, 60, 0.92) 0%, rgba(13, 22, 37, 0.98) 100%)';
                                let contentTopPadding = 3;

                                if (plan.isPopular) {
                                    cardBorder = '1px solid rgba(87, 173, 255, 0.75)';
                                    cardBackground = 'linear-gradient(160deg, rgba(17, 43, 84, 0.9) 0%, rgba(11, 22, 42, 0.95) 100%)';
                                    cardShadow = '0 14px 30px rgba(5, 11, 23, 0.42)';
                                    hoverBackground = 'linear-gradient(160deg, rgba(26, 62, 118, 0.95) 0%, rgba(14, 31, 61, 0.98) 100%)';
                                }

                                const highlightedCardStyles = {
                                    borderColor: 'rgba(111, 199, 255, 0.95)',
                                    background: hoverBackground,
                                    boxShadow: '0 0 0 1px rgba(128, 210, 255, 0.35), 0 20px 38px rgba(4, 10, 21, 0.45)'
                                };

                                return (
                                    <Box
                                        key={plan.durationMonths}
                                        sx={{
                                            position: 'relative',
                                            overflow: 'visible',
                                            display: 'flex',
                                            flexDirection: 'column',
                                            height: '100%',
                                            transition: 'transform 300ms ease',
                                            '@media (hover: hover) and (pointer: fine)': {
                                                '&:hover': {
                                                    transform: 'scale(1.03)'
                                                },
                                                '&:hover .subscriptionPlanCard': highlightedCardStyles
                                            },
                                            '@media (hover: none), (pointer: coarse)': {
                                                '&:active': {
                                                    transform: 'scale(1.03)'
                                                },
                                                '&:active .subscriptionPlanCard': highlightedCardStyles
                                            }
                                        }}
                                    >
                                        {plan.isPopular && (
                                            <Box
                                                sx={{
                                                    position: 'absolute',
                                                    top: 0,
                                                    left: '50%',
                                                    transform: 'translate(-50%, -50%)',
                                                    zIndex: 10,
                                                    pointerEvents: 'none'
                                                }}
                                            >
                                                <Box
                                                    component='span'
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
                                                        boxShadow: '0 10px 24px rgba(0, 0, 0, 0.3)'
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
                                                borderRadius: 3,
                                                border: cardBorder,
                                                background: cardBackground,
                                                boxShadow: cardShadow,
                                                cursor: 'pointer',
                                                transition: 'box-shadow 300ms ease, border-color 300ms ease, background 300ms ease'
                                            }}
                                        >
                                            <CardContent
                                                sx={{
                                                    p: 3,
                                                    pt: contentTopPadding,
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
                                            onClick={onRedeemAccessKey}
                                            disabled={isRedeemingKey || !accessKey.trim()}
                                            size='large'
                                        >
                                            {isRedeemingKey ? 'Redeeming...' : 'Redeem Key'}
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
