import React, { type FC, type ReactNode } from 'react';

import { type ContentRequestStatus } from 'utils/contentRequestsApi';

import RequestStatusBadge from './RequestStatusBadge';

export interface RequestCardMetaRow {
    label: string
    value: ReactNode
}

interface RequestCardProps {
    title: string
    status?: ContentRequestStatus
    metaRows: RequestCardMetaRow[]
    actions?: ReactNode
}

const RequestCard: FC<RequestCardProps> = ({
    title,
    status,
    metaRows,
    actions
}) => (
    <article className='requestCard'>
        <div className='requestCardHeader'>
            <h3 className='requestCardTitle'>{title}</h3>
            {!!status && <RequestStatusBadge status={status} />}
        </div>
        <dl className='requestCardMeta'>
            {metaRows.map(row => (
                <React.Fragment key={row.label}>
                    <dt className='requestCardMetaLabel'>{row.label}</dt>
                    <dd className='requestCardMetaValue'>{row.value}</dd>
                </React.Fragment>
            ))}
        </dl>
        {!!actions && (
            <div className='requestCardActions'>{actions}</div>
        )}
    </article>
);

export default RequestCard;
