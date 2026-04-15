import Stack from '@mui/material/Stack';
import React, { type FC } from 'react';
import { useLocation } from 'react-router-dom';

import { appRouter, PUBLIC_PATHS } from 'components/router/appRouter';
import AppToolbar from 'components/toolbar/AppToolbar';
import ServerButton from 'components/toolbar/ServerButton';
import { useApi } from 'hooks/useApi';
import { isExpiredSubscriptionUser } from 'utils/subscription';

import RemotePlayButton from './RemotePlayButton';
import SyncPlayButton from './SyncPlayButton';
import SearchButton from './SearchButton';
import UserViewNav from './userViews/UserViewNav';
import DownloadAppButton from 'components/toolbar/DownloadAppButton';

interface AppToolbarProps {
    isDrawerAvailable: boolean
    isDrawerOpen: boolean
    onDrawerButtonClick: (event: React.MouseEvent<HTMLElement>) => void
}

const ExperimentalAppToolbar: FC<AppToolbarProps> = ({
    isDrawerAvailable,
    isDrawerOpen,
    onDrawerButtonClick
}) => {
    const location = useLocation();
    const { user } = useApi();

    // The video osd does not show the standard toolbar
    if (location.pathname === '/video') return null;

    // Only show the back button in apps when appropriate
    const isBackButtonAvailable = window.NativeShell && appRouter.canGoBack(location.pathname);

    // Check if the current path is a public path to hide user content
    const isPublicPath = PUBLIC_PATHS.includes(location.pathname);
    const isSubscriptionPage = location.pathname === '/subscription';
    const isSimpleHeader = isExpiredSubscriptionUser(user) || isSubscriptionPage;

    return (
        <AppToolbar
            buttons={!isPublicPath && !isSimpleHeader && (
                <>
                    <SyncPlayButton />
                    <RemotePlayButton />
                    <SearchButton />
                    <DownloadAppButton />
                </>
            )}
            isDrawerAvailable={isDrawerAvailable}
            isDrawerOpen={isDrawerOpen}
            onDrawerButtonClick={onDrawerButtonClick}
            isBackButtonAvailable={isBackButtonAvailable}
            isUserMenuAvailable={!isPublicPath}
        >
            {!isDrawerAvailable && (
                <Stack
                    direction='row'
                    spacing={0.5}
                >
                    <ServerButton />

                    {!isPublicPath && (
                        <UserViewNav />
                    )}
                </Stack>
            )}
        </AppToolbar>
    );
};

export default ExperimentalAppToolbar;
