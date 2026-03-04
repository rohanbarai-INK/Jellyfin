import { Command, RefreshCw, ShieldCheck } from 'lucide-react';
import React, { useCallback, useEffect, useState } from 'react';

import BulkKeyGenerator from './components/BulkKeyGenerator';
import CohortChart from './components/CohortChart';
import ExpiryRadar from './components/ExpiryRadar';
import KeyAnalytics from './components/KeyAnalytics';
import OverviewCards from './components/OverviewCards';
import SystemHealthPanel from './components/SystemHealth';
import {
    fetchDashboardSnapshot,
    fetchExpiringUsers,
    type CohortData,
    type ExpiringUser,
    type ExpiryRadar as ExpiryRadarType,
    type KeyStats,
    type OverviewStats,
    type SystemHealth as SystemHealthData
} from './data/api';
import { cn } from './utils/cn';

import './components/subscriptionCommandCenter.scss';

function Skeleton({ className = '' }: Readonly<{ className?: string }>) {
    return <div className={cn('scc-skeleton', className)} />;
}

export const SubscriptionCommandCenter = () => {
    const [ overview, setOverview ] = useState<OverviewStats | null>(null);
    const [ radar, setRadar ] = useState<ExpiryRadarType | null>(null);
    const [ expiringUsers, setExpiringUsers ] = useState<ExpiringUser[]>([]);
    const [ keyStats, setKeyStats ] = useState<KeyStats | null>(null);
    const [ cohorts, setCohorts ] = useState<CohortData[]>([]);
    const [ health, setHealth ] = useState<SystemHealthData | null>(null);
    const [ loadingUsers, setLoadingUsers ] = useState(false);
    const [ refreshing, setRefreshing ] = useState(false);
    const [ refreshError, setRefreshError ] = useState('');
    const [ lastUpdated, setLastUpdated ] = useState<Date>(new Date());

    const loadAll = useCallback(async () => {
        const snapshot = await fetchDashboardSnapshot();

        setOverview(snapshot.overview);
        setRadar(snapshot.radar);
        setKeyStats(snapshot.keyStats);
        setCohorts(snapshot.cohorts);
        setHealth(snapshot.health);
        setLastUpdated(new Date());
    }, []);

    const handleRefresh = useCallback(async () => {
        setRefreshError('');
        setRefreshing(true);
        try {
            await loadAll();
        } catch (errorValue: unknown) {
            console.error('[subscription-command-center] refresh failed', errorValue);
            setRefreshError('Failed to refresh dashboard data.');
        } finally {
            setRefreshing(false);
        }
    }, [ loadAll ]);

    const handleFetchUsers = useCallback(async (days: number) => {
        setLoadingUsers(true);
        const users = await fetchExpiringUsers(days);
        setExpiringUsers(users);
        setLoadingUsers(false);
    }, []);

    const onRefreshClick = useCallback(() => {
        void handleRefresh();
    }, [ handleRefresh ]);

    useEffect(() => {
        void (async () => {
            try {
                await loadAll();
            } catch (errorValue: unknown) {
                console.error('[subscription-command-center] initial load failed', errorValue);
            }
        })();
    }, [ loadAll ]);

    const loading = !overview || !radar || !keyStats;

    return (
        <div className='subscriptionCommandCenterRoot'>
            <div className='scc-topBar'>
                <div className='scc-topBarInner'>
                    <div className='scc-topBarLeft'>
                        <div className='scc-commandBadge'>
                            <Command width={16} height={16} />
                        </div>
                        <p className='scc-topBarTitle'>Subscription Command Center</p>
                        <span className='scc-adminBadge'>
                            <ShieldCheck width={12} height={12} />
                            ADMIN
                        </span>
                    </div>

                    <div className='scc-topBarRight'>
                        <span className='scc-updatedText'>Updated {lastUpdated.toLocaleTimeString()}</span>
                        <button
                            type='button'
                            className='scc-refreshButton'
                            disabled={refreshing}
                            onClick={onRefreshClick}
                        >
                            <RefreshCw width={14} height={14} className={cn(refreshing && 'scc-spin')} />
                            Refresh
                        </button>
                    </div>
                </div>
            </div>

            <main className='scc-main'>
                <header className='scc-pageHeader'>
                    <div className='scc-pageHeaderRow'>
                        <div className='scc-pageHeaderLeft'>
                            <div className='scc-commandBadge scc-pageCommandBadge'>
                                <Command width={16} height={16} />
                            </div>
                            <h1 className='scc-pageTitle'>Subscription Command Center</h1>
                        </div>
                        <button
                            type='button'
                            className='scc-refreshButton scc-pageRefreshButton'
                            disabled={refreshing}
                            onClick={onRefreshClick}
                        >
                            <RefreshCw width={14} height={14} className={cn(refreshing && 'scc-spin')} />
                            Refresh
                        </button>
                    </div>
                    <p className='scc-pageSubtitle'>
                        Centralized control panel for subscription analytics and key management.
                    </p>
                    {!!refreshError && (
                        <p className='scc-refreshError'>{refreshError}</p>
                    )}
                </header>

                {loading ? (
                    <div className='scc-overviewGrid'>
                        {Array.from({ length: 8 }).map((_, index) => (
                            <Skeleton key={`overview-skeleton-${index + 1}`} className='scc-overviewSkeleton' />
                        ))}
                    </div>
                ) : (
                    <OverviewCards data={overview} />
                )}

                <div className='scc-twoColGrid'>
                    {loading ? (
                        <>
                            <Skeleton className='scc-sectionSkeletonTall' />
                            <Skeleton className='scc-sectionSkeletonTall' />
                        </>
                    ) : (
                        <>
                            <ExpiryRadar
                                radar={radar}
                                expiringUsers={expiringUsers}
                                onFetchUsers={handleFetchUsers}
                                loadingUsers={loadingUsers}
                            />
                            <KeyAnalytics data={keyStats} />
                        </>
                    )}
                </div>

                <BulkKeyGenerator />

                {/* Reseller Performance intentionally disabled until backend logic is available. */}
                {cohorts.length === 0 ? (
                    <Skeleton className='scc-sectionSkeletonTall' />
                ) : (
                    <CohortChart data={cohorts} />
                )}

                {!health ? (
                    <Skeleton className='scc-sectionSkeletonSmall' />
                ) : (
                    <SystemHealthPanel data={health} />
                )}

                <footer className='scc-footerNotice'>
                    <ShieldCheck width={14} height={14} />
                    <span>
                        This dashboard provides analytics and tooling only. Existing subscription lifecycle logic
                        (AccessKeyService, ExpiryDate calculation, ExpiredSubscriptionMiddleware, /Keys/Redeem,
                        /Keys/CurrentSubscription) remains untouched.
                    </span>
                </footer>
            </main>
        </div>
    );
};

SubscriptionCommandCenter.displayName = 'SubscriptionCommandCenter';
