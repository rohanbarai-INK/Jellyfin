import React, { type FC, useMemo } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestRow } from 'utils/contentRequestsApi';

import AdminRequestActions from './AdminRequestActions';
import RequestCard, { type RequestCardMetaRow } from './RequestCard';
import RequestEmptyState from './RequestEmptyState';
import RequestPagination from './RequestPagination';
import RequestStatusBadge from './RequestStatusBadge';
import RequestTable, { type RequestTableColumn } from './RequestTable';
import useRequestIsMobileLayout from './useRequestIsMobileLayout';

interface AdminRequestTableProps {
    rows: ContentRequestRow[]
    pageIndex: number
    pageSize: number
    totalRecordCount: number
    isBusy: boolean
    onApprove: (requestId: string) => void
    onReject: (requestId: string) => void
    onComplete: (row: ContentRequestRow) => void
    onPageChange: (nextPageIndex: number) => void
}

const formatDateTime = (value: string) => {
    if (!value) {
        return '-';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
        return value;
    }

    return parsed.toLocaleString();
};

const AdminRequestTable: FC<AdminRequestTableProps> = ({
    rows,
    pageIndex,
    pageSize,
    totalRecordCount,
    isBusy,
    onApprove,
    onReject,
    onComplete,
    onPageChange
}) => {
    const preferCardsLayout = useRequestIsMobileLayout();

    const columns: RequestTableColumn<ContentRequestRow>[] = useMemo(() => ([
        {
            key: 'user',
            label: globalize.translate('HeaderUsers'),
            render: row => row.username || '-'
        },
        {
            key: 'title',
            label: globalize.translate('LabelTitle'),
            className: 'requestColTitle',
            render: row => (
                <span className='requestCellTruncate' title={row.title}>
                    {row.title}
                </span>
            )
        },
        {
            key: 'type',
            label: globalize.translate('LabelType'),
            className: 'requestColType',
            render: row => row.type
        },
        {
            key: 'season',
            label: globalize.translate('Season'),
            className: 'requestColSeason',
            render: row => row.seasonNumber ?? '-'
        },
        {
            key: 'date',
            label: globalize.translate('DateAdded'),
            className: 'requestColDate',
            render: row => formatDateTime(row.requestedAt)
        },
        {
            key: 'status',
            label: globalize.translate('LabelStatus'),
            className: 'requestColStatus',
            render: row => (
                <RequestStatusBadge status={row.status} />
            )
        },
        {
            key: 'action',
            label: globalize.translate('RequestActionColumn'),
            className: 'requestColAction',
            render: row => (
                <AdminRequestActions
                    row={row}
                    isBusy={isBusy}
                    onApprove={onApprove}
                    onReject={onReject}
                    onComplete={onComplete}
                />
            )
        }
    ]), [ isBusy, onApprove, onComplete, onReject ]);

    return (
        <>
            {!preferCardsLayout && (
                <RequestTable
                    columns={columns}
                    rows={rows}
                    rowKey={row => row.id}
                    emptyMessage={globalize.translate('RequestNoRows')}
                />
            )}

            <div className={`requestCardList${preferCardsLayout ? '' : ' requestMobileOnly'}`}>
                {rows.map(row => {
                    const metaRows: RequestCardMetaRow[] = [
                        {
                            label: globalize.translate('HeaderUsers'),
                            value: row.username || '-'
                        },
                        {
                            label: globalize.translate('LabelType'),
                            value: row.type
                        },
                        {
                            label: globalize.translate('Season'),
                            value: row.seasonNumber ?? '-'
                        },
                        {
                            label: globalize.translate('DateAdded'),
                            value: formatDateTime(row.requestedAt)
                        }
                    ];

                    return (
                        <RequestCard
                            key={row.id}
                            title={row.title}
                            status={row.status}
                            metaRows={metaRows}
                            actions={(
                                <AdminRequestActions
                                    row={row}
                                    isBusy={isBusy}
                                    onApprove={onApprove}
                                    onReject={onReject}
                                    onComplete={onComplete}
                                />
                            )}
                        />
                    );
                })}
                {rows.length === 0 && (
                    <RequestEmptyState message={globalize.translate('RequestNoRows')} />
                )}
            </div>
            <RequestPagination
                pageIndex={pageIndex}
                pageSize={pageSize}
                totalRecordCount={totalRecordCount}
                isBusy={isBusy}
                onPageChange={onPageChange}
            />
        </>
    );
};

export default AdminRequestTable;
