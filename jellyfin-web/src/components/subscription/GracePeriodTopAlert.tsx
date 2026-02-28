import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined';
import CloseIcon from '@mui/icons-material/Close';
import Box from '@mui/material/Box';
import Button from '@mui/material/Button';
import IconButton from '@mui/material/IconButton';
import Stack from '@mui/material/Stack';
import Typography from '@mui/material/Typography';
import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { useApi } from 'hooks/useApi';
import { isInGraceSubscriptionUser } from 'utils/subscription';

type GraceAwareUserRecord = {
    ExpiryDate?: string | null
    expiryDate?: string | null
    GraceDaysRemaining?: number | null
    graceDaysRemaining?: number | null
    localUser?: GraceAwareUserRecord
    LocalUser?: GraceAwareUserRecord
};

type GraceAwareUser = GraceAwareUserRecord | null | undefined;

const getLocalUser = (user: GraceAwareUser): GraceAwareUser => (
    user?.localUser || user?.LocalUser || user
);

const parseGraceDaysRemaining = (user: GraceAwareUser) => {
    const localUser = getLocalUser(user);
    const parsedValue = Number(localUser?.GraceDaysRemaining ?? localUser?.graceDaysRemaining);
    return Number.isFinite(parsedValue) && parsedValue >= 0 ? Math.trunc(parsedValue) : 0;
};

const parseExpiryLabel = (user: GraceAwareUser) => {
    const localUser = getLocalUser(user);
    const rawDate = localUser?.ExpiryDate ?? localUser?.expiryDate;
    if (!rawDate) {
        return null;
    }

    const parsedDate = new Date(rawDate);
    if (Number.isNaN(parsedDate.getTime())) {
        return null;
    }

    return parsedDate.toLocaleDateString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    });
};

const GracePeriodTopAlert = () => {
    const { user } = useApi();
    const graceUser = user as GraceAwareUser;
    const [ isDismissed, setIsDismissed ] = useState(false);
    const showGraceAlert = useMemo(
        () => isInGraceSubscriptionUser(graceUser),
        [ graceUser ]);

    const graceDaysRemaining = useMemo(
        () => parseGraceDaysRemaining(graceUser),
        [ graceUser ]);

    const expiryLabel = useMemo(
        () => parseExpiryLabel(graceUser),
        [ graceUser ]);

    useEffect(() => {
        if (!showGraceAlert) {
            setIsDismissed(false);
        }
    }, [ showGraceAlert ]);

    const onDismiss = useCallback(() => {
        setIsDismissed(true);
    }, []);

    if (!showGraceAlert || isDismissed) {
        return null;
    }

    const graceDaysText = graceDaysRemaining <= 0
        ? 'today'
        : `${graceDaysRemaining} day${graceDaysRemaining === 1 ? '' : 's'}`;

    return (
        <Box
            sx={{
                position: 'fixed',
                top: {
                    xs: 64,
                    md: 72
                },
                left: '50%',
                transform: 'translateX(-50%)',
                width: 'min(860px, calc(100vw - 24px))',
                zIndex: 1900,
                pointerEvents: 'none'
            }}
        >
            <Box
                role='alert'
                sx={{
                    pointerEvents: 'auto',
                    border: '2px solid #1d4ed8',
                    backgroundColor: '#dbeafe',
                    color: '#1e3a8a',
                    p: {
                        xs: 1.4,
                        md: 1.8
                    },
                    pr: {
                        xs: 5.5,
                        md: 6.5
                    },
                    boxShadow: '4px 4px 0 0 rgba(30, 58, 138, 0.6)',
                    borderRadius: 1.5,
                    position: 'relative'
                }}
            >
                <IconButton
                    aria-label='Close grace period alert'
                    size='small'
                    onClick={onDismiss}
                    sx={{
                        position: 'absolute',
                        top: 6,
                        right: 6,
                        color: '#1e3a8a',
                        backgroundColor: 'rgba(191, 219, 254, 0.9)',
                        border: '1px solid rgba(30, 64, 175, 0.75)',
                        boxShadow: '2px 2px 0 0 rgba(30, 64, 175, 0.45)',
                        '&:hover': {
                            backgroundColor: 'rgba(147, 197, 253, 0.95)'
                        }
                    }}
                >
                    <CloseIcon sx={{ fontSize: 16 }} />
                </IconButton>
                <Stack
                    direction={{
                        xs: 'column',
                        sm: 'row'
                    }}
                    spacing={1.25}
                    alignItems={{
                        xs: 'flex-start',
                        sm: 'center'
                    }}
                >
                    <Stack direction='row' spacing={1.2} alignItems='flex-start' sx={{ flex: 1 }}>
                        <InfoOutlinedIcon sx={{ mt: 0.2, fontSize: 18 }} />
                        <Box sx={{ minWidth: 0 }}>
                            <Typography component='strong' sx={{ display: 'block', fontWeight: 800, lineHeight: 1.3 }}>
                                {`Grace period ends in ${graceDaysText}. Keep your access uninterrupted.`}
                            </Typography>
                            <Typography sx={{ mt: 0.4, fontSize: 14, fontWeight: 600, lineHeight: 1.35 }}>
                                {`Renew your subscription now to continue Movies, TV Shows, and requests without lock.`}
                                {expiryLabel ? ` Previous plan expired on ${expiryLabel}.` : ''}
                            </Typography>
                        </Box>
                    </Stack>
                    <Button
                        component='a'
                        href='#/subscription'
                        variant='contained'
                        size='small'
                        onClick={onDismiss}
                        sx={{
                            textTransform: 'none',
                            fontWeight: 800,
                            color: '#ffffff',
                            backgroundColor: '#1d4ed8',
                            border: '1px solid #1e40af',
                            boxShadow: '3px 3px 0 0 rgba(30, 64, 175, 0.55)',
                            '&:hover': {
                                backgroundColor: '#1e40af'
                            }
                        }}
                    >
                        Renew Subscription
                    </Button>
                </Stack>
            </Box>
        </Box>
    );
};

export default GracePeriodTopAlert;
