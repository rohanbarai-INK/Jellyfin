import {
    AlertTriangle,
    Archive,
    CheckCircle2,
    Clock,
    IndianRupee,
    Key,
    Users,
    XCircle
} from 'lucide-react';
import React from 'react';

import { type OverviewStats } from '../data/api';
import { cn } from '../utils/cn';

interface OverviewCardsProps {
    data: OverviewStats;
    onOpenMetric?: (metricId: OverviewMetricId) => void;
}

export type OverviewMetricId =
    | 'activeUsers'
    | 'graceUsers'
    | 'expiredUsers'
    | 'expiringSoon'
    | 'totalRevenue'
    | 'keysGenerated'
    | 'keysRedeemed'
    | 'unusedKeys';

interface CardDef {
    id: OverviewMetricId;
    label: string;
    value: number | string;
    rawValue: number;
    subtext: string;
    icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
    valueClass: string;
    iconClass: string;
}

const OverviewCards = ({ data, onOpenMetric }: OverviewCardsProps) => {
    const redemptionRate = data.keysGenerated > 0 ? Math.round((data.keysRedeemed / data.keysGenerated) * 100) : 0;

    const cards: CardDef[] = [
        {
            id: 'activeUsers',
            label: 'Active Users',
            value: data.activeUsers,
            rawValue: data.activeUsers,
            subtext: 'Currently active subscriptions',
            icon: Users,
            valueClass: 'scc-text-emerald',
            iconClass: 'scc-iconBadge-emerald'
        },
        {
            id: 'graceUsers',
            label: 'Grace Period',
            value: data.graceUsers,
            rawValue: data.graceUsers,
            subtext: 'In grace window',
            icon: Clock,
            valueClass: 'scc-text-amber',
            iconClass: 'scc-iconBadge-amber'
        },
        {
            id: 'expiredUsers',
            label: 'Expired Users',
            value: data.expiredUsers,
            rawValue: data.expiredUsers,
            subtext: 'Subscription lapsed',
            icon: XCircle,
            valueClass: 'scc-text-red',
            iconClass: 'scc-iconBadge-red'
        },
        {
            id: 'expiringSoon',
            label: 'Expiring Soon',
            value: data.expiringSoon,
            rawValue: data.expiringSoon,
            subtext: 'Within next 7 days',
            icon: AlertTriangle,
            valueClass: 'scc-text-orange',
            iconClass: 'scc-iconBadge-orange'
        },
        {
            id: 'totalRevenue',
            label: 'Total Revenue',
            value: `\u20B9${data.totalRevenue.toLocaleString('en-IN')}`,
            rawValue: data.totalRevenue,
            subtext: 'All-time earnings',
            icon: IndianRupee,
            valueClass: 'scc-text-violet',
            iconClass: 'scc-iconBadge-violet'
        },
        {
            id: 'keysGenerated',
            label: 'Keys Generated',
            value: data.keysGenerated,
            rawValue: data.keysGenerated,
            subtext: 'Total access keys',
            icon: Key,
            valueClass: 'scc-text-sky',
            iconClass: 'scc-iconBadge-sky'
        },
        {
            id: 'keysRedeemed',
            label: 'Keys Redeemed',
            value: data.keysRedeemed,
            rawValue: data.keysRedeemed,
            subtext: `${redemptionRate}% redemption rate`,
            icon: CheckCircle2,
            valueClass: 'scc-text-teal',
            iconClass: 'scc-iconBadge-teal'
        },
        {
            id: 'unusedKeys',
            label: 'Unused Keys',
            value: data.unusedKeys,
            rawValue: data.unusedKeys,
            subtext: 'Pending redemption',
            icon: Archive,
            valueClass: 'scc-text-slate',
            iconClass: 'scc-iconBadge-slate'
        }
    ];

    return (
        <section className='scc-section'>
            <h2 className='scc-sectionTitle'>
                <span className='scc-sectionAccent scc-accent-violet' />
                Overview Snapshot
            </h2>
            <div className='scc-overviewGrid'>
                {cards.map((card) => {
                    const Icon = card.icon;
                    const canOpen = card.rawValue > 0;

                    return (
                        <button
                            key={card.label}
                            type='button'
                            className={cn('scc-overviewCard', !canOpen && 'scc-overviewCardDisabled')}
                            onClick={() => {
                                if (!canOpen) {
                                    return;
                                }

                                onOpenMetric?.(card.id);
                            }}
                            aria-disabled={!canOpen}
                        >
                            <div className='scc-overviewCardHead'>
                                <span className='scc-overviewCardLabel'>{card.label}</span>
                                <span className={cn('scc-iconBadge', card.iconClass)}>
                                    <Icon width={14} height={14} />
                                </span>
                            </div>
                            <p className={cn('scc-overviewCardValue', card.valueClass)}>{card.value}</p>
                            <p className='scc-overviewCardSubtext'>{card.subtext}</p>
                        </button>
                    );
                })}
            </div>
        </section>
    );
};

export default OverviewCards;
