import CheckCircleOutlineIcon from '@mui/icons-material/CheckCircleOutline';
import CloseIcon from '@mui/icons-material/Close';
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutline';
import AutorenewIcon from '@mui/icons-material/Autorenew';
import systemLoaderGif from 'assets/branding/system-loader.gif';
import Box from '@mui/material/Box';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import React, { useEffect, useMemo, useState } from 'react';

import { useApi } from 'hooks/useApi';

type AutoHealStatus = {
    state: string
    message: string
    failureReason?: string | null
    retryAfterSeconds?: number
};

const HEALTHY_STATE = 'healthy';
const RECONNECTING_STATE = 'reconnecting';
const RECOVERED_STATE = 'recovered';
const DEGRADED_STATE = 'degraded';
const UNAVAILABLE_STATE = 'unavailable';

const NORMAL_POLL_MS = 10000;
const RECOVERY_POLL_MS = 3000;

const parseStatus = (value: unknown): AutoHealStatus => {
    const source = (value && typeof value === 'object') ? value as Record<string, unknown> : {};
    const rawState = source.State ?? source.state;
    const rawMessage = source.Message ?? source.message;
    const rawFailureReason = source.FailureReason ?? source.failureReason;
    const rawRetryAfterSeconds = source.RetryAfterSeconds ?? source.retryAfterSeconds;

    return {
        state: typeof rawState === 'string' ? rawState.toLowerCase() : HEALTHY_STATE,
        message: typeof rawMessage === 'string' ? rawMessage : '',
        failureReason: typeof rawFailureReason === 'string' ? rawFailureReason : null,
        retryAfterSeconds: typeof rawRetryAfterSeconds === 'number' ? rawRetryAfterSeconds : undefined
    };
};

const getFallbackMessage = (state: string) => {
    switch (state) {
        case RECONNECTING_STATE:
            return 'Media storage is reconnecting. Please wait 30 seconds.';
        case RECOVERED_STATE:
            return 'Playback service has been restored. Please try again.';
        case DEGRADED_STATE:
            return 'Service is temporarily unavailable. Please try again in 1-2 minutes.';
        case UNAVAILABLE_STATE:
            return 'Server is unavailable, please check after some time.';
        default:
            return '';
    }
};

const MediaMountRecoveryAlert = () => {
    const { __legacyApiClient__, user } = useApi();
    const [ status, setStatus ] = useState<AutoHealStatus | null>(null);
    const [ dismissedSignature, setDismissedSignature ] = useState<string | null>(null);

    useEffect(() => {
        if (!__legacyApiClient__ || !user?.Id) {
            setStatus(null);
            return undefined;
        }

        let isCancelled = false;
        let timeoutId: number | undefined;

        const pollStatus = async () => {
            let nextDelay = NORMAL_POLL_MS;
            try {
                const response = await __legacyApiClient__.getJSON(__legacyApiClient__.getUrl('System/AutoHeal/Status'));
                if (!isCancelled) {
                    const parsedStatus = parseStatus(response);
                    setStatus(parsedStatus);
                    nextDelay = parsedStatus.state === RECONNECTING_STATE ? RECOVERY_POLL_MS : NORMAL_POLL_MS;
                }
            } catch (error) {
                if (!isCancelled) {
                    const unavailableStatus = {
                        state: UNAVAILABLE_STATE,
                        message: getFallbackMessage(UNAVAILABLE_STATE),
                        failureReason: null,
                        retryAfterSeconds: 10
                    };
                    setStatus(unavailableStatus);
                    nextDelay = RECOVERY_POLL_MS;
                }

                console.debug('[MediaMountRecoveryAlert] status poll failed', error);
            } finally {
                if (!isCancelled) {
                    timeoutId = window.setTimeout(() => {
                        void pollStatus();
                    }, nextDelay);
                }
            }
        };

        void pollStatus();

        return () => {
            isCancelled = true;
            if (timeoutId) {
                window.clearTimeout(timeoutId);
            }
        };
    }, [ __legacyApiClient__, user?.Id ]);

    const statusSignature = useMemo(() => {
        if (!status) {
            return null;
        }

        return [
            status.state,
            status.message,
            status.failureReason ?? '',
            status.retryAfterSeconds ?? ''
        ].join('|');
    }, [ status ]);

    const visibleStatus = useMemo(() => {
        if (!status) {
            return null;
        }

        if (status.state === HEALTHY_STATE) {
            return null;
        }

        if (statusSignature && dismissedSignature === statusSignature) {
            return null;
        }

        return {
            ...status,
            message: status.message || getFallbackMessage(status.state)
        };
    }, [ dismissedSignature, status, statusSignature ]);

    if (!visibleStatus) {
        return null;
    }

    const isRecovering = visibleStatus.state === RECONNECTING_STATE;
    const isRecovered = visibleStatus.state === RECOVERED_STATE;
    const isDegraded = visibleStatus.state === DEGRADED_STATE;
    const isUnavailable = visibleStatus.state === UNAVAILABLE_STATE;
    const isRetryingState = isRecovering || isUnavailable;

    const accentColor = isRecovered ? '#047857' : isRetryingState ? '#1d4ed8' : '#b45309';
    const backgroundColor = isRecovered ? '#d1fae5' : isRetryingState ? '#eaf2ff' : '#fef3c7';
    const borderColor = isRecovered ? '#047857' : isRetryingState ? '#1d4ed8' : '#b45309';
    const icon = isRetryingState
        ? (
            <Box
                component='img'
                src={systemLoaderGif}
                alt='Server status'
                sx={{
                    width: 54,
                    height: 54,
                    objectFit: 'contain',
                    display: 'block'
                }}
            />
        )
        : isRecovered
            ? <CheckCircleOutlineIcon sx={{ fontSize: 20, color: accentColor }} />
            : <ErrorOutlineIcon sx={{ fontSize: 20, color: accentColor }} />;

    return (
        <Box
            sx={{
                position: 'fixed',
                top: {
                    xs: 74,
                    md: 88
                },
                left: '50%',
                transform: 'translateX(-50%)',
                width: 'min(640px, calc(100vw - 28px))',
                zIndex: 1950,
                pointerEvents: 'none'
            }}
        >
            <Box
                role='alert'
                sx={{
                    pointerEvents: 'auto',
                    border: `2px solid ${borderColor}`,
                    backgroundColor,
                    color: accentColor,
                    p: {
                        xs: 1.4,
                        md: 1.8
                    },
                    pr: 5.5,
                    boxShadow: `4px 4px 0 0 ${accentColor}66`,
                    borderRadius: 1.5,
                    position: 'relative'
                }}
            >
                <IconButton
                    aria-label='Close media recovery alert'
                    size='small'
                    onClick={() => setDismissedSignature(statusSignature)}
                    sx={{
                        position: 'absolute',
                        top: 6,
                        right: 6,
                        color: accentColor,
                        backgroundColor: 'rgba(255,255,255,0.55)',
                        border: `1px solid ${accentColor}66`,
                        '&:hover': {
                            backgroundColor: 'rgba(255,255,255,0.82)'
                        }
                    }}
                >
                    <CloseIcon sx={{ fontSize: 16 }} />
                </IconButton>
                <Stack direction='row' spacing={1.25} alignItems='flex-start'>
                    <Box sx={{ mt: 0.2, minWidth: 54, display: 'flex', justifyContent: 'center' }}>
                        {icon}
                    </Box>
                    <Box sx={{ minWidth: 0, flex: 1 }}>
                        <Typography component='strong' sx={{ display: 'block', fontWeight: 800, lineHeight: 1.3 }}>
                            {visibleStatus.message}
                        </Typography>
                        <Typography sx={{ mt: 0.45, fontSize: 14, fontWeight: 600, lineHeight: 1.35 }}>
                            {isRecovering && 'Playback requests may fail briefly while storage reconnects.'}
                            {isRecovered && 'The media path is available again and playback can be retried now.'}
                            {isDegraded && 'Playback is still blocked because the media path has not recovered yet.'}
                            {isUnavailable && 'The app is retrying in the background and will reconnect automatically.'}
                        </Typography>
                        {visibleStatus.failureReason && isDegraded && (
                            <Typography sx={{ mt: 0.65, fontSize: 12.5, lineHeight: 1.35, opacity: 0.92 }}>
                                {visibleStatus.failureReason}
                            </Typography>
                        )}
                        {isRetryingState && (
                            <Stack direction='row' spacing={0.8} alignItems='center' sx={{ mt: 0.85 }}>
                                <AutorenewIcon sx={{ fontSize: 15 }} />
                                <Typography sx={{ fontSize: 12.5, fontWeight: 700 }}>
                                    Retrying automatically in the background.
                                </Typography>
                            </Stack>
                        )}
                    </Box>
                </Stack>
            </Box>
        </Box>
    );
};

export default MediaMountRecoveryAlert;
