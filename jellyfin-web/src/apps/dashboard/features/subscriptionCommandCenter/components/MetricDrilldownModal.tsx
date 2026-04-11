import { ChevronLeft, ChevronRight, X } from 'lucide-react';
import React, { useMemo } from 'react';

import {
    type AdminAccessKeyDetailRow,
    type AdminSubscriptionUserDetailRow,
    type ExpiringUser
} from '../data/api';
import { cn } from '../utils/cn';
import { type OverviewMetricId } from './OverviewCards';

type DrilldownResult =
    | { kind: 'keys'; rows: AdminAccessKeyDetailRow[] }
    | { kind: 'users'; rows: AdminSubscriptionUserDetailRow[] }
    | { kind: 'expiring'; rows: ExpiringUser[] };

interface MetricDrilldownModalProps {
    metric: OverviewMetricId;
    open: boolean;
    loading: boolean;
    error: string;
    startIndex: number;
    limit: number;
    totalRecordCount: number;
    result: DrilldownResult | null;
    onClose: () => void;
    onPageChange: (nextStartIndex: number) => void;
}

function formatDate(value: string): string {
    if (!value) {
        return '-';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return '-';
    }

    return date.toLocaleDateString('en-IN', {
        day: '2-digit',
        month: 'short',
        year: 'numeric'
    });
}

function formatDateTime(value: string): string {
    if (!value) {
        return '-';
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return '-';
    }

    return date.toLocaleString('en-IN', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function metricTitle(metric: OverviewMetricId): { title: string; subtitle: string } {
    switch (metric) {
        case 'activeUsers':
            return { title: 'Active Users', subtitle: 'Currently active subscriptions' };
        case 'graceUsers':
            return { title: 'Grace Period Users', subtitle: 'In grace window' };
        case 'expiredUsers':
            return { title: 'Expired Users', subtitle: 'Subscription lapsed' };
        case 'expiringSoon':
            return { title: 'Expiring Soon', subtitle: 'Within next 7 days' };
        case 'totalRevenue':
            return { title: 'Total Revenue', subtitle: 'All-time earnings (latest redemptions)' };
        case 'keysGenerated':
            return { title: 'Keys Generated', subtitle: 'Latest generated keys' };
        case 'keysRedeemed':
            return { title: 'Keys Redeemed', subtitle: 'Latest redeemed keys' };
        case 'unusedKeys':
            return { title: 'Unused Keys', subtitle: 'Pending redemption' };
        default:
            return { title: 'Details', subtitle: '' };
    }
}

const MetricDrilldownModal = ({
    metric,
    open,
    loading,
    error,
    startIndex,
    limit,
    totalRecordCount,
    result,
    onClose,
    onPageChange
}: MetricDrilldownModalProps) => {
    const { title, subtitle } = useMemo(() => metricTitle(metric), [ metric ]);

    const pageStart = totalRecordCount === 0 ? 0 : startIndex + 1;
    const pageEnd = Math.min(totalRecordCount, startIndex + (result?.rows.length ?? 0));
    const canPrev = startIndex > 0;
    const canNext = startIndex + limit < totalRecordCount;

    if (!open) {
        return null;
    }

    return (
        <div className='scc-modalBackdrop' role='presentation'>
            <div
                className={cn('scc-modalPanel', 'scc-drilldownPanel')}
                role='dialog'
                aria-modal='true'
                aria-labelledby='scc-drilldown-title'
            >
                <div className='scc-modalHeader'>
                    <div>
                        <h3 id='scc-drilldown-title' className='scc-modalTitle'>{title}</h3>
                        <p className='scc-modalSubtitle'>{subtitle}</p>
                    </div>
                    <button
                        type='button'
                        className='scc-iconButton'
                        onClick={onClose}
                        aria-label='Close details panel'
                    >
                        <X width={16} height={16} />
                    </button>
                </div>

                <div className='scc-modalBody'>
                    {!!error && (
                        <div className='scc-modalEmpty'>
                            {error}
                        </div>
                    )}

                    {loading && !error && (
                        <div className='scc-modalEmpty'>
                            <div className='scc-spinner' />
                            <span>Loading details...</span>
                        </div>
                    )}

                    {!loading && !error && totalRecordCount === 0 && (
                        <div className='scc-modalEmpty'>
                            No rows for this metric.
                        </div>
                    )}

                    {!loading && !error && totalRecordCount > 0 && result?.kind === 'keys' && (
                        <div className='scc-tableWrap'>
                            <table className='scc-table'>
                                <thead>
                                    <tr>
                                        <th>Key</th>
                                        <th>Duration</th>
                                        <th>Created</th>
                                        <th>Status</th>
                                        <th>Redeemed By</th>
                                        <th>Redeemed At</th>
                                        <th>Amount</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {result.rows.map((row) => (
                                        <tr key={row.key}>
                                            <td className='scc-mono'>{row.key}</td>
                                            <td>{row.durationMonths === 1 ? '1 Month' : `${row.durationMonths} Months`}</td>
                                            <td>{formatDateTime(row.createdAt)}</td>
                                            <td>{row.isRedeemed ? 'Redeemed' : 'Unused'}</td>
                                            <td className='scc-mono'>{row.redeemedByUsername || '-'}</td>
                                            <td>{formatDateTime(row.redeemedAt)}</td>
                                            <td>{row.redeemedAmount ? `\u20B9${row.redeemedAmount.toLocaleString('en-IN')}` : '-'}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {!loading && !error && totalRecordCount > 0 && result?.kind === 'users' && (
                        <div className='scc-tableWrap'>
                            <table className='scc-table'>
                                <thead>
                                    <tr>
                                        <th>Username</th>
                                        <th>Plan</th>
                                        <th>Expiry</th>
                                        <th>Days Left</th>
                                        <th>Grace Left</th>
                                        <th>State</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {result.rows.map((row) => (
                                        <tr key={row.userId}>
                                            <td className='scc-mono'>{row.username}</td>
                                            <td>{row.plan || 'N/A'}</td>
                                            <td>{row.expiryDate ? formatDate(row.expiryDate) : '-'}</td>
                                            <td>{row.daysRemaining >= 0 ? `${row.daysRemaining}d` : '-'}</td>
                                            <td>{row.graceDaysRemaining > 0 ? `${row.graceDaysRemaining}d` : '-'}</td>
                                            <td>{row.state}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {!loading && !error && totalRecordCount > 0 && result?.kind === 'expiring' && (
                        <div className='scc-tableWrap'>
                            <table className='scc-table scc-modalTable'>
                                <thead>
                                    <tr>
                                        <th>Username</th>
                                        <th>Plan</th>
                                        <th>Expiry Date</th>
                                        <th>Days Left</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {result.rows.map((row) => (
                                        <tr key={row.userId}>
                                            <td className='scc-mono'>{row.username}</td>
                                            <td>{row.plan}</td>
                                            <td>{formatDate(row.expiryDate)}</td>
                                            <td>
                                                <span className={cn('scc-daysBadge', row.daysRemaining <= 1 ? 'scc-urgencyRed' : row.daysRemaining <= 3 ? 'scc-urgencyOrange' : row.daysRemaining <= 7 ? 'scc-urgencyAmber' : 'scc-urgencySky')}>
                                                    {row.daysRemaining === 0 ? 'Today' : `${row.daysRemaining}d`}
                                                </span>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>

                <div className='scc-modalFooter'>
                    <div className='scc-modalFooterLeft'>
                        {totalRecordCount > 0 && (
                            <span className='scc-modalPagerText'>
                                Showing {pageStart}-{pageEnd} of {totalRecordCount}
                            </span>
                        )}
                    </div>
                    <div className='scc-modalFooterRight'>
                        <button
                            type='button'
                            className='scc-pagerButton'
                            onClick={() => onPageChange(Math.max(0, startIndex - limit))}
                            disabled={!canPrev || loading}
                        >
                            <ChevronLeft width={14} height={14} />
                            Prev
                        </button>
                        <button
                            type='button'
                            className='scc-pagerButton'
                            onClick={() => onPageChange(startIndex + limit)}
                            disabled={!canNext || loading}
                        >
                            Next
                            <ChevronRight width={14} height={14} />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MetricDrilldownModal;

