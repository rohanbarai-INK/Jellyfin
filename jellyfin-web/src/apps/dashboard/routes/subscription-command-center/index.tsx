import React from 'react';

import { SubscriptionCommandCenter } from 'apps/dashboard/features/subscriptionCommandCenter';
import Page from 'components/Page';

export const Component = () => {
    return (
        <Page
            id='dashboardSubscriptionCommandCenterPage'
            className='mainAnimatedPage type-interior'
            title='Subscription Command Center'
        >
            <div className='content-primary'>
                <SubscriptionCommandCenter />
            </div>
        </Page>
    );
};

Component.displayName = 'DashboardSubscriptionCommandCenterPage';
