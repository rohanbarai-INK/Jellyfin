import Alert from '@mui/material/Alert';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Chip from '@mui/material/Chip';
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

const getServerErrorMessage = async (err: unknown): Promise<string | undefined> => {
    if (err instanceof Response) {
        if (err.status === 401) {
            return 'Unauthorized request. Sign out and sign in again, then retry.';
        }

        if (err.status === 404) {
            return 'Redeem endpoint not found. Make sure the backend has access key support.';
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

    if (statusCode === 401) {
        return 'Unauthorized request. Sign out and sign in again, then retry.';
    }

    if (statusCode === 404) {
        return 'Redeem endpoint not found. Make sure the backend has access key support.';
    }

    if (typeof error.text === 'function') {
        const responseText = await error.text();
        if (responseText.trim()) {
            return responseText;
        }
    }

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

    return undefined;
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
    } = useNamedConfiguration<Partial<SubscriptionPricing>>(SUBSCRIPTION_CONFIG_KEY);

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
                    py: {
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
                        <Stack spacing={1}>
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
                                gap: 2
                            }}
                        >
                            {SUBSCRIPTION_PLANS.map(plan => (
                                <Card
                                    key={plan.durationMonths}
                                    sx={{
                                        position: 'relative',
                                        borderRadius: 3,
                                        border: plan.isPopular ? '1px solid rgba(87, 173, 255, 0.75)' : '1px solid rgba(255, 255, 255, 0.12)',
                                        background: plan.isPopular
                                            ? 'linear-gradient(160deg, rgba(17, 43, 84, 0.9) 0%, rgba(11, 22, 42, 0.95) 100%)'
                                            : 'linear-gradient(160deg, rgba(17, 24, 37, 0.86) 0%, rgba(9, 14, 25, 0.95) 100%)',
                                        boxShadow: plan.isPopular
                                            ? '0 14px 30px rgba(5, 11, 23, 0.42)'
                                            : '0 10px 22px rgba(4, 8, 16, 0.3)'
                                    }}
                                >
                                    <CardContent sx={{ p: 3 }}>
                                        <Stack spacing={1.5}>
                                            <Box
                                                sx={{
                                                    display: 'flex',
                                                    justifyContent: 'space-between',
                                                    alignItems: 'center'
                                                }}
                                            >
                                                <Typography variant='h6' sx={{ fontWeight: 700 }}>
                                                    {plan.title}
                                                </Typography>
                                                {plan.isPopular && (
                                                    <Chip
                                                        label='Most Popular'
                                                        size='small'
                                                        color='primary'
                                                        sx={{ fontWeight: 600 }}
                                                    />
                                                )}
                                            </Box>
                                            <Typography sx={{ opacity: 0.8 }}>
                                                {plan.description}
                                            </Typography>
                                            <Typography
                                                variant='h4'
                                                sx={{
                                                    mt: 1,
                                                    fontWeight: 700
                                                }}
                                            >
                                                Rs {getPlanPrice(pricing, plan.durationMonths)}
                                            </Typography>
                                            <Typography sx={{ opacity: 0.75 }}>
                                                {plan.durationMonths} month{plan.durationMonths === 1 ? '' : 's'}
                                            </Typography>
                                        </Stack>
                                    </CardContent>
                                </Card>
                            ))}
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
