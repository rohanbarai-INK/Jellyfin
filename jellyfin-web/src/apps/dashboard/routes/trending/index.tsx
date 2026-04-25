import React from 'react';

import TrendingAdmin from 'apps/dashboard/features/trending';
import Page from 'components/Page';

export const Component = () => {
    return (
        <Page
            id='dashboardTrendingPage'
            className='mainAnimatedPage type-interior'
            title='Trending Promotions'
        >
            <div className='content-primary'>
                <TrendingAdmin />
            </div>
        </Page>
    );
};

Component.displayName = 'DashboardTrendingPage';
