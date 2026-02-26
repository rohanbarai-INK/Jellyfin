import React, { type FC, type ReactNode } from 'react';

interface RequestHeaderProps {
    title: string
    subtitle?: string
    actions?: ReactNode
}

const RequestHeader: FC<RequestHeaderProps> = ({
    title,
    subtitle,
    actions
}) => (
    <div className='requestHeader'>
        <div className='requestHeaderText'>
            <h2 className='requestHeaderTitle'>{title}</h2>
            {!!subtitle && (
                <p className='requestHeaderSubtitle'>{subtitle}</p>
            )}
        </div>
        {actions}
    </div>
);

export default RequestHeader;
