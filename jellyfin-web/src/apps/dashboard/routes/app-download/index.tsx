import React from 'react';

import AppDownloadAdmin from 'apps/dashboard/features/appDownload';
import Page from 'components/Page';

export const Component = () => {
    return (
        <Page
            id='dashboardAppDownloadPage'
            className='mainAnimatedPage type-interior'
            title='App Downloads'
        >
            <div className='content-primary'>
                <AppDownloadAdmin />
            </div>
        </Page>
    );
};

Component.displayName = 'DashboardAppDownloadPage';
