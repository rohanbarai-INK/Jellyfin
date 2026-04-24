import React, { type FC, useMemo, useState } from 'react';

import globalize from 'lib/globalize';
import { type PublicContentRequestRow } from 'utils/contentRequestsApi';

import RequestCard, { type RequestCardMetaRow } from './RequestCard';
import RequestEmptyState from './RequestEmptyState';
import RequestHeader from './RequestHeader';
import RequestStatusBadge from './RequestStatusBadge';
import RequestTable, { type RequestTableColumn } from './RequestTable';
import useRequestIsMobileLayout from './useRequestIsMobileLayout';

interface PublicRequestPoolProps {
    rows: PublicContentRequestRow[]
    currentUserId: string
}

type PublicPoolTypeFilter = 'All' | 'Movie' | 'Series';
type PublicPoolScopeFilter = 'Current' | 'All';

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

const isCurrentPoolStatus = (status: PublicContentRequestRow['status']) => (
    status === 'Pending' || status === 'Approved'
);

const PublicRequestPool: FC<PublicRequestPoolProps> = ({
    rows,
    currentUserId
}) => {
    const preferCardsLayout = useRequestIsMobileLayout();
    const [ search, setSearch ] = useState('');
    const [ typeFilter, setTypeFilter ] = useState<PublicPoolTypeFilter>('All');
    const [ scopeFilter, setScopeFilter ] = useState<PublicPoolScopeFilter>('Current');

    const poolRows = useMemo(() => rows.filter(row => row.userId !== currentUserId), [ currentUserId, rows ]);

    const filteredRows = useMemo(() => {
        const normalizedSearch = search.trim().toLowerCase();

        return poolRows
            .filter(row => scopeFilter === 'All' || isCurrentPoolStatus(row.status))
            .filter(row => typeFilter === 'All' || row.type === typeFilter)
            .filter(row => {
                if (!normalizedSearch) {
                    return true;
                }

                return row.title.toLowerCase().includes(normalizedSearch)
                    || row.username.toLowerCase().includes(normalizedSearch)
                    || row.type.toLowerCase().includes(normalizedSearch)
                    || row.status.toLowerCase().includes(normalizedSearch);
            });
    }, [ poolRows, scopeFilter, search, typeFilter ]);

    const columns: RequestTableColumn<PublicContentRequestRow>[] = useMemo(() => ([
        {
            key: 'username',
            label: globalize.translate('RequestRequesterColumn'),
            className: 'requestColRequester',
            render: row => (
                <span className='requestCellTruncate' title={row.username || '-'}>
                    {row.username || '-'}
                </span>
            )
        },
        {
            key: 'title',
            label: globalize.translate('LabelTitle'),
            className: 'requestColPoolTitle',
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
        }
    ]), []);

    return (
        <section className='requestSection'>
            <RequestHeader
                title={globalize.translate('RequestPoolTitle')}
                subtitle={globalize.translate('RequestPoolSubtitle')}
            />
            <div className='requestTableTools requestFilterTools'>
                <label className='requestSearchField' aria-label={globalize.translate('Search')}>
                    <span className='requestSearchFieldIcon' aria-hidden='true' />
                    <span className='requestSearchInteractionRing' aria-hidden='true' />
                    <input
                        className='requestInput requestSearchInput'
                        type='search'
                        value={search}
                        onChange={event => setSearch(event.target.value)}
                        placeholder={globalize.translate('RequestPoolSearchPlaceholder')}
                        aria-label={globalize.translate('Search')}
                    />
                </label>
                <div className='requestFilterRow'>
                    <label className='requestFilterField'>
                        <span>{globalize.translate('RequestPoolTypeFilter')}</span>
                        <select
                            className='requestInput requestSelect'
                            value={typeFilter}
                            onChange={event => setTypeFilter(event.target.value as PublicPoolTypeFilter)}
                        >
                            <option value='All'>{globalize.translate('RequestFilterAllTypes')}</option>
                            <option value='Movie'>{globalize.translate('Movie')}</option>
                            <option value='Series'>{globalize.translate('Series')}</option>
                        </select>
                    </label>
                    <label className='requestFilterField'>
                        <span>{globalize.translate('RequestPoolScopeFilter')}</span>
                        <select
                            className='requestInput requestSelect'
                            value={scopeFilter}
                            onChange={event => setScopeFilter(event.target.value as PublicPoolScopeFilter)}
                        >
                            <option value='Current'>{globalize.translate('RequestPoolScopeCurrent')}</option>
                            <option value='All'>{globalize.translate('RequestPoolScopeAll')}</option>
                        </select>
                    </label>
                </div>
            </div>

            {!preferCardsLayout && (
                <RequestTable
                    columns={columns}
                    rows={filteredRows}
                    rowKey={row => row.id}
                    emptyMessage={globalize.translate('RequestPoolNoRows')}
                    emptyTitle={globalize.translate('RequestPoolNoRows')}
                />
            )}

            <div className={`requestCardList${preferCardsLayout ? '' : ' requestMobileOnly'}`}>
                {filteredRows.map(row => {
                    const metaRows: RequestCardMetaRow[] = [
                        {
                            label: globalize.translate('RequestRequesterColumn'),
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
                        />
                    );
                })}
                {filteredRows.length === 0 && (
                    <RequestEmptyState
                        message={globalize.translate('RequestPoolNoRows')}
                        title={globalize.translate('RequestPoolNoRows')}
                    />
                )}
            </div>
        </section>
    );
};

export default PublicRequestPool;
