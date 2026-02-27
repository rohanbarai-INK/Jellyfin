import React, { type FC, useMemo, useState } from 'react';

import globalize from 'lib/globalize';
import { type ContentRequestRow } from 'utils/contentRequestsApi';

import RequestCard, { type RequestCardMetaRow } from './RequestCard';
import RequestEmptyState from './RequestEmptyState';
import RequestHeader from './RequestHeader';
import RequestStatusBadge from './RequestStatusBadge';
import RequestTable, { type RequestTableColumn } from './RequestTable';
import useRequestIsMobileLayout from './useRequestIsMobileLayout';

interface RequestListProps {
    rows: ContentRequestRow[]
}

interface ParsedRequestTitle {
    primaryTitle: string
    requestReference: string
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

const parseRequestTitle = (value: string): ParsedRequestTitle => {
    const normalizedValue = value.trim();
    const lines = normalizedValue
        .split(/\r?\n/g)
        .map(line => line.trim())
        .filter(Boolean);

    const firstLine = lines[0] || normalizedValue;
    const secondaryText = lines.slice(1).join(' ').trim();

    if (/^\d{6,}$/.test(secondaryText)) {
        return {
            primaryTitle: firstLine || '-',
            requestReference: secondaryText
        };
    }

    const match = firstLine.match(/^(.*?)(?:\s+(\d{6,}))$/);
    if (match) {
        const [, parsedTitle, parsedReference ] = match;

        return {
            primaryTitle: (parsedTitle || '').trim() || '-',
            requestReference: parsedReference || ''
        };
    }

    return {
        primaryTitle: firstLine || '-',
        requestReference: ''
    };
};

const RequestList: FC<RequestListProps> = ({ rows }) => {
    const [ search, setSearch ] = useState('');
    const preferCardsLayout = useRequestIsMobileLayout();

    const filteredRows = useMemo(() => {
        const searchTerm = search.trim().toLowerCase();
        if (!searchTerm) {
            return rows;
        }

        return rows.filter(row => (
            row.title.toLowerCase().includes(searchTerm)
            || row.type.toLowerCase().includes(searchTerm)
            || row.status.toLowerCase().includes(searchTerm)
        ));
    }, [ rows, search ]);

    const columns: RequestTableColumn<ContentRequestRow>[] = useMemo(() => ([
        {
            key: 'title',
            label: globalize.translate('LabelTitle'),
            className: 'requestColTitle',
            render: row => {
                const parsedTitle = parseRequestTitle(row.title).primaryTitle;

                return (
                    <span className='requestCellTruncate' title={parsedTitle}>
                        {parsedTitle}
                    </span>
                );
            }
        },
        {
            key: 'requestReference',
            label: globalize.translate('RequestIdLabel'),
            className: 'requestColRequestId',
            render: row => parseRequestTitle(row.title).requestReference || '-'
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
        }
    ]), []);

    return (
        <section className='requestSection'>
            <RequestHeader title={globalize.translate('RequestMyRequestsTitle')} />
            <div className='requestTableTools'>
                <label className='requestSearchField' aria-label={globalize.translate('Search')}>
                    <span className='requestSearchFieldIcon' aria-hidden='true' />
                    <span className='requestSearchInteractionRing' aria-hidden='true' />
                    <input
                        className='requestInput requestSearchInput'
                        type='search'
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                        placeholder={globalize.translate('RequestSearchPlaceholder')}
                        aria-label={globalize.translate('Search')}
                    />
                </label>
            </div>

            {!preferCardsLayout && (
                <RequestTable
                    columns={columns}
                    rows={filteredRows}
                    rowKey={row => row.id}
                    emptyMessage={globalize.translate('RequestNoRows')}
                    emptyTitle={globalize.translate('RequestEmptyTitle')}
                    emptySubtitle={globalize.translate('RequestEmptySubtitle')}
                    emptyActionText={globalize.translate('RequestEmptyCta')}
                    emptyActionHref='#requestCreateSection'
                />
            )}

            <div className={`requestCardList${preferCardsLayout ? '' : ' requestMobileOnly'}`}>
                {filteredRows.map(row => {
                    const parsedTitle = parseRequestTitle(row.title);
                    const metaRows: RequestCardMetaRow[] = [
                        {
                            label: globalize.translate('RequestIdLabel'),
                            value: parsedTitle.requestReference || '-'
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
                            title={parsedTitle.primaryTitle}
                            status={row.status}
                            metaRows={metaRows}
                        />
                    );
                })}
                {filteredRows.length === 0 && (
                    <RequestEmptyState
                        message={globalize.translate('RequestNoRows')}
                        title={globalize.translate('RequestEmptyTitle')}
                        subtitle={globalize.translate('RequestEmptySubtitle')}
                        actionText={globalize.translate('RequestEmptyCta')}
                        actionHref='#requestCreateSection'
                    />
                )}
            </div>
        </section>
    );
};

export default RequestList;
