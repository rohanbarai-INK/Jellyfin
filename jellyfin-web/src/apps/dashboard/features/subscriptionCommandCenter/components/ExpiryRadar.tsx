import { ChevronRight, Clock, X } from 'lucide-react';
import React, { type MouseEvent, useCallback, useMemo, useState } from 'react';

import { type ExpiringUser, type ExpiryRadar as ExpiryRadarData } from '../data/api';
import { cn } from '../utils/cn';

interface ExpiryRadarProps {
    radar: ExpiryRadarData;
    expiringUsers: ExpiringUser[];
    onFetchUsers: (days: number) => void;
    loadingUsers: boolean;
}

type TierKey = keyof ExpiryRadarData;

interface Tier {
    label: string;
    key: TierKey;
    days: number;
    textColor: string;
    barColor: string;
    badge: string;
}

const TIERS: Tier[] = [
    {
        label: 'Next 24 hours',
        key: 'next24h',
        days: 1,
        textColor: 'scc-text-red',
        barColor: 'linear-gradient(90deg, #ef4444, #dc2626)',
        badge: 'CRITICAL'
    },
    {
        label: 'Next 3 days',
        key: 'next3days',
        days: 3,
        textColor: 'scc-text-orange',
        barColor: 'linear-gradient(90deg, #f97316, #ea580c)',
        badge: 'HIGH'
    },
    {
        label: 'Next 7 days',
        key: 'next7days',
        days: 7,
        textColor: 'scc-text-amber',
        barColor: 'linear-gradient(90deg, #f59e0b, #d97706)',
        badge: 'MEDIUM'
    },
    {
        label: 'Next 30 days',
        key: 'next30days',
        days: 30,
        textColor: 'scc-text-sky',
        barColor: 'linear-gradient(90deg, #0ea5e9, #0284c7)',
        badge: 'LOW'
    }
];

function urgencyColor(days: number): string {
    if (days <= 1) {
        return 'scc-urgencyRed';
    }

    if (days <= 3) {
        return 'scc-urgencyOrange';
    }

    if (days <= 7) {
        return 'scc-urgencyAmber';
    }

    return 'scc-urgencySky';
}

const ExpiryRadar = ({
    radar,
    expiringUsers,
    onFetchUsers,
    loadingUsers
}: ExpiryRadarProps) => {
    const [ showModal, setShowModal ] = useState(false);
    const [ selectedDays, setSelectedDays ] = useState(7);

    const maxValue = Math.max(radar.next30days, 1);

    const filteredUsers = useMemo(
        () => expiringUsers.filter((user) => user.daysRemaining <= selectedDays),
        [ expiringUsers, selectedDays ]
    );

    const openUsers = useCallback((days: number) => {
        setSelectedDays(days);
        onFetchUsers(days);
        setShowModal(true);
    }, [ onFetchUsers ]);

    const onTierViewClick = useCallback((event: MouseEvent<HTMLButtonElement>) => {
        const days = Number(event.currentTarget.dataset.days);

        if (!Number.isNaN(days)) {
            openUsers(days);
        }
    }, [ openUsers ]);

    const onDefaultOpenClick = useCallback(() => {
        openUsers(7);
    }, [ openUsers ]);

    const onCloseModal = useCallback(() => {
        setShowModal(false);
    }, []);

    return (
        <>
            <section className='scc-section scc-card'>
                <h2 className='scc-sectionTitle'>
                    <span className='scc-sectionAccent scc-accent-orange' />
                    Expiry Radar
                </h2>

                <div className='scc-radarTiers'>
                    {TIERS.map((tier) => {
                        const count = radar[tier.key];
                        const width = Math.min(100, (count / maxValue) * 100);

                        return (
                            <div key={tier.key} className='scc-radarTier'>
                                <div className='scc-radarTierTop'>
                                    <div className='scc-radarTierLabels'>
                                        <p className='scc-radarTierLabel'>{tier.label}</p>
                                        <span className='scc-radarBadge'>{tier.badge}</span>
                                    </div>
                                    <div className='scc-radarTierStats'>
                                        <p className={cn('scc-radarTierCount', tier.textColor)}>{count}</p>
                                        <button
                                            type='button'
                                            className='scc-radarViewButton'
                                            data-days={tier.days}
                                            onClick={onTierViewClick}
                                        >
                                            View
                                            <ChevronRight width={12} height={12} />
                                        </button>
                                    </div>
                                </div>
                                <div className='scc-progressTrack'>
                                    <div
                                        className='scc-progressFill'
                                        style={{
                                            width: `${width}%`,
                                            background: tier.barColor
                                        }}
                                    />
                                </div>
                            </div>
                        );
                    })}
                </div>

                <button
                    type='button'
                    className='scc-radarOpenButton'
                    onClick={onDefaultOpenClick}
                >
                    <Clock width={15} height={15} />
                    View Expiring Users
                </button>
            </section>

            {showModal && (
                <div className='scc-modalBackdrop' role='presentation'>
                    <div
                        className='scc-modalPanel'
                        role='dialog'
                        aria-modal='true'
                        aria-labelledby='scc-expiring-users-title'
                    >
                        <div className='scc-modalHeader'>
                            <div>
                                <h3 id='scc-expiring-users-title' className='scc-modalTitle'>Expiring Users</h3>
                                <p className='scc-modalSubtitle'>Within next {selectedDays} day(s)</p>
                            </div>
                            <button
                                type='button'
                                className='scc-iconButton'
                                onClick={onCloseModal}
                                aria-label='Close expiring users modal'
                            >
                                <X width={16} height={16} />
                            </button>
                        </div>

                        <div className='scc-modalBody'>
                            {loadingUsers && (
                                <div className='scc-modalEmpty'>
                                    <div className='scc-spinner' />
                                    <span>Loading expiring users...</span>
                                </div>
                            )}

                            {!loadingUsers && filteredUsers.length === 0 && (
                                <div className='scc-modalEmpty'>
                                    No users expiring in this window.
                                </div>
                            )}

                            {!loadingUsers && filteredUsers.length > 0 && (
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
                                            {filteredUsers.map((user) => (
                                                <tr key={user.userId}>
                                                    <td className='scc-mono'>{user.username}</td>
                                                    <td>{user.plan}</td>
                                                    <td>
                                                        {new Date(user.expiryDate).toLocaleDateString('en-IN', {
                                                            day: '2-digit',
                                                            month: 'short',
                                                            year: 'numeric'
                                                        })}
                                                    </td>
                                                    <td>
                                                        <span className={cn('scc-daysBadge', urgencyColor(user.daysRemaining))}>
                                                            {user.daysRemaining === 0 ? 'Today' : `${user.daysRemaining}d`}
                                                        </span>
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

export default ExpiryRadar;
