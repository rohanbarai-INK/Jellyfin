import React from 'react';
import { Outlet } from 'react-router-dom';

import AppBody from 'components/AppBody';
import FeatureAnnouncementsRoot from 'components/featureAnnouncements/FeatureAnnouncementsRoot';
import RequestNotificationPopup from 'components/contentRequests/RequestNotificationPopup';
import CustomCss from 'components/CustomCss';
import ThemeCss from 'components/ThemeCss';

export default function AppLayout() {
    return (
        <>
            <AppBody>
                <Outlet />
            </AppBody>
            <RequestNotificationPopup />
            <FeatureAnnouncementsRoot />
            <ThemeCss />
            <CustomCss />
        </>
    );
}
