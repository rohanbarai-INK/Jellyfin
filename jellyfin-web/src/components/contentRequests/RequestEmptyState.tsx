import React, { type FC } from 'react';

interface RequestEmptyStateProps {
    message: string
    title?: string
    subtitle?: string
    actionText?: string
    actionHref?: string
}

const RequestEmptyState: FC<RequestEmptyStateProps> = ({
    message,
    title,
    subtitle
}) => (
    <div className='requestEmptyState'>
        <div className='requestEmptyStateIcon' aria-hidden='true'>o</div>
        <div className='requestEmptyStateTitle'>{title || message}</div>
        {!!subtitle && (
            <div className='requestEmptyStateSubtitle'>{subtitle}</div>
        )}
        {(!subtitle && message !== title) && (
            <div className='requestEmptyStateSubtitle'>{message}</div>
        )}
    </div>
);

export default RequestEmptyState;
