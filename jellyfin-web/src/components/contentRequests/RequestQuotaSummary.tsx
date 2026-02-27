import React, { type FC } from 'react';

import inactiveBannerIconGif from 'assets/branding/request-subscription-inactive-v2.gif';
import globalize from 'lib/globalize';
import { type ContentRequestQuotaSummary } from 'utils/contentRequestsApi';

import { type RequestSubscriptionUiState } from './types';

interface RequestQuotaSummaryProps {
    quota: ContentRequestQuotaSummary
    subscriptionUiState: RequestSubscriptionUiState
    isQuotaExhausted: boolean
}

const SubscriptionStateIcon: FC = () => (
    <picture className='requestStateBannerIcon requestStateBannerIconMedia' aria-hidden='true'>
        <img
            className='requestStateBannerIconImage'
            src={inactiveBannerIconGif}
            alt=''
            width={20}
            height={20}
        />
    </picture>
);

const RequestQuotaSummary: FC<RequestQuotaSummaryProps> = ({
    quota,
    subscriptionUiState,
    isQuotaExhausted
}) => {
    const showInactiveMessage = subscriptionUiState === 'active' && !quota.isSubscriptionActive;
    const showQuotaExhaustedMessage = subscriptionUiState === 'active'
        && quota.isSubscriptionActive
        && isQuotaExhausted;

    return (
        <div className='requestQuotaSummary'>
            {subscriptionUiState === 'grace' && (
                <div className='requestStateBanner warning'>
                    <SubscriptionStateIcon />
                    <span className='requestStateBannerText'>{globalize.translate('RequestGraceMessage')}</span>
                    <a className='requestRenewButton' href='#/subscription'>
                        {globalize.translate('RequestExpiredRenewCta')}
                    </a>
                </div>
            )}

            {subscriptionUiState === 'expired' && (
                <div className='requestStateBanner error'>
                    <SubscriptionStateIcon />
                    <span className='requestStateBannerText'>{globalize.translate('RequestExpiredMessage')}</span>
                    <a className='requestRenewButton' href='#/subscription'>
                        {globalize.translate('RequestExpiredRenewCta')}
                    </a>
                </div>
            )}

            {showInactiveMessage && (
                <div className='requestStateBanner error'>
                    <SubscriptionStateIcon />
                    <span className='requestStateBannerText'>{globalize.translate('RequestSubscriptionInactive')}</span>
                    <a className='requestRenewButton' href='#/subscription'>
                        {globalize.translate('RequestExpiredRenewCta')}
                    </a>
                </div>
            )}

            {showQuotaExhaustedMessage && (
                <div className='requestStateBanner info'>
                    <span className='requestStateBannerIcon' aria-hidden='true'>i</span>
                    <span className='requestStateBannerText'>{globalize.translate('RequestQuotaExhaustedMessage')}</span>
                </div>
            )}
        </div>
    );
};

export default RequestQuotaSummary;
