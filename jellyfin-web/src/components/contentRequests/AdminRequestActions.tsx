import React, { type FC } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestRow } from 'utils/contentRequestsApi';

interface AdminRequestActionsProps {
    row: ContentRequestRow
    isBusy: boolean
    onApprove: (requestId: string) => void
    onReject: (requestId: string) => void
    onComplete: (row: ContentRequestRow) => void
}

const AdminRequestActions: FC<AdminRequestActionsProps> = ({
    row,
    isBusy,
    onApprove,
    onReject,
    onComplete
}) => {
    if (row.status !== 'Pending' && row.status !== 'Approved') {
        return <span className='adminRequestActionPlaceholder'>-</span>;
    }

    return (
        <div className='adminRequestActions'>
            {row.status === 'Pending' && (
                <button
                    className='requestActionButton action-approve'
                    type='button'
                    onClick={() => onApprove(row.id)}
                    disabled={isBusy}
                >
                    {globalize.translate('ButtonApprove')}
                </button>
            )}
            {row.status === 'Approved' && (
                <button
                    className='requestActionButton action-complete'
                    type='button'
                    onClick={() => onComplete(row)}
                    disabled={isBusy}
                >
                    {globalize.translate('RequestCompleteAction')}
                </button>
            )}
            <button
                className='requestActionButton action-reject'
                type='button'
                onClick={() => onReject(row.id)}
                disabled={isBusy}
            >
                {globalize.translate('Reject')}
            </button>
        </div>
    );
};

export default AdminRequestActions;
