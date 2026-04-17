import React from 'react';

import AnnouncementAdmin from 'apps/dashboard/features/announcement';
import Page from 'components/Page';

export const Component = () => {
    return (
        <Page
            id='dashboardAnnouncementPage'
            className='mainAnimatedPage type-interior'
            title='Announcement'
        >
            <div className='content-primary'>
                <AnnouncementAdmin />
            </div>
        </Page>
    );
};

Component.displayName = 'DashboardAnnouncementPage';
