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
            <SubscriptionCommandCenter />
        </Page>
    );
};

Component.displayName = 'DashboardSubscriptionCommandCenterPage';
