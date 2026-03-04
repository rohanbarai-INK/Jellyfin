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
}

interface CardDef {
    label: string;
    value: number | string;
    subtext: string;
    icon: React.ComponentType<React.SVGProps<SVGSVGElement>>;
    valueClass: string;
    iconClass: string;
}

const OverviewCards = ({ data }: OverviewCardsProps) => {
    const redemptionRate = data.keysGenerated > 0 ? Math.round((data.keysRedeemed / data.keysGenerated) * 100) : 0;

    const cards: CardDef[] = [
        {
            label: 'Active Users',
            value: data.activeUsers,
            subtext: 'Currently active subscriptions',
            icon: Users,
            valueClass: 'scc-text-emerald',
            iconClass: 'scc-iconBadge-emerald'
        },
        {
            label: 'Grace Period',
            value: data.graceUsers,
            subtext: 'In grace window',
            icon: Clock,
            valueClass: 'scc-text-amber',
            iconClass: 'scc-iconBadge-amber'
        },
        {
            label: 'Expired Users',
            value: data.expiredUsers,
            subtext: 'Subscription lapsed',
            icon: XCircle,
            valueClass: 'scc-text-red',
            iconClass: 'scc-iconBadge-red'
        },
        {
            label: 'Expiring Soon',
            value: data.expiringSoon,
            subtext: 'Within next 7 days',
            icon: AlertTriangle,
            valueClass: 'scc-text-orange',
            iconClass: 'scc-iconBadge-orange'
        },
        {
            label: 'Total Revenue',
            value: `\u20B9${data.totalRevenue.toLocaleString('en-IN')}`,
            subtext: 'All-time earnings',
            icon: IndianRupee,
            valueClass: 'scc-text-violet',
            iconClass: 'scc-iconBadge-violet'
        },
        {
            label: 'Keys Generated',
            value: data.keysGenerated,
            subtext: 'Total access keys',
            icon: Key,
            valueClass: 'scc-text-sky',
            iconClass: 'scc-iconBadge-sky'
        },
        {
            label: 'Keys Redeemed',
            value: data.keysRedeemed,
            subtext: `${redemptionRate}% redemption rate`,
            icon: CheckCircle2,
            valueClass: 'scc-text-teal',
            iconClass: 'scc-iconBadge-teal'
        },
        {
            label: 'Unused Keys',
            value: data.unusedKeys,
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

                    return (
                        <div key={card.label} className='scc-overviewCard'>
                            <div className='scc-overviewCardHead'>
                                <span className='scc-overviewCardLabel'>{card.label}</span>
                                <span className={cn('scc-iconBadge', card.iconClass)}>
                                    <Icon width={14} height={14} />
                                </span>
                            </div>
                            <p className={cn('scc-overviewCardValue', card.valueClass)}>{card.value}</p>
                            <p className='scc-overviewCardSubtext'>{card.subtext}</p>
                        </div>
                    );
                })}
            </div>
        </section>
    );
};

export default OverviewCards;
