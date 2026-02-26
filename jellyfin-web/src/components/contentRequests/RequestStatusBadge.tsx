import React, { type FC } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestStatus } from 'utils/contentRequestsApi';

interface RequestStatusBadgeProps {
    status: ContentRequestStatus
}

const getStatusLabel = (status: ContentRequestStatus) => {
    if (status === 'Completed') {
        return globalize.translate('RequestStatusFulfilled');
    }

    return status;
};

const RequestStatusBadge: FC<RequestStatusBadgeProps> = ({ status }) => (
    <span className={`requestStatusBadge status-${status.toLowerCase()}`}>
        {getStatusLabel(status)}
    </span>
);

export default RequestStatusBadge;
