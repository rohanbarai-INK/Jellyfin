import React, { type FC, type PropsWithChildren } from 'react';

interface RequestPageContainerProps {
    className?: string
}

const RequestPageContainer: FC<PropsWithChildren<RequestPageContainerProps>> = ({
    children,
    className = ''
}) => (
    <div className={`content-primary requestPageContainer${className ? ` ${className}` : ''}`}>
        <div className='requestPageContainerInner'>
            {children}
        </div>
    </div>
);

export default RequestPageContainer;
