import {
    Activity,
    Minus,
    TrendingDown,
    TrendingUp,
    Users
} from 'lucide-react';
import React from 'react';

import { type SystemHealth as SystemHealthData } from '../data/api';
import { cn } from '../utils/cn';

interface SystemHealthPanelProps {
    data: SystemHealthData;
}

interface HealthStyle {
    text: string;
    bg: string;
    ring: string;
    bar: string;
    label: string;
}

type HealthType = 'renewal' | 'growth';

interface ScoreStyle {
    stroke: string;
    label: string;
    pillClass: string;
}

function getHealthColor(value: number, type: HealthType): HealthStyle {
    if (type === 'renewal') {
        if (value >= 75) {
            return {
                text: 'scc-text-emerald',
                bg: 'scc-healthBg-emerald',
                ring: 'scc-healthRing-emerald',
                bar: 'scc-healthBar-emerald',
                label: 'Healthy'
            };
        }

        if (value >= 55) {
            return {
                text: 'scc-text-amber',
                bg: 'scc-healthBg-amber',
                ring: 'scc-healthRing-amber',
                bar: 'scc-healthBar-amber',
                label: 'Fair'
            };
        }

        return {
            text: 'scc-text-red',
            bg: 'scc-healthBg-red',
            ring: 'scc-healthRing-red',
            bar: 'scc-healthBar-red',
            label: 'At Risk'
        };
    }

    if (value > 0) {
        return {
            text: 'scc-text-emerald',
            bg: 'scc-healthBg-emerald',
            ring: 'scc-healthRing-emerald',
            bar: 'scc-healthBar-emerald',
            label: 'Growing'
        };
    }

    if (value === 0) {
        return {
            text: 'scc-text-slate',
            bg: 'scc-healthBg-slate',
            ring: 'scc-healthRing-slate',
            bar: 'scc-healthBar-slate',
            label: 'Flat'
        };
    }

    return {
        text: 'scc-text-red',
        bg: 'scc-healthBg-red',
        ring: 'scc-healthRing-red',
        bar: 'scc-healthBar-red',
        label: 'Declining'
    };
}

const GrowthIcon = ({ value }: { value: number }) => {
    if (value > 0) {
        return <TrendingUp width={16} height={16} />;
    }

    if (value < 0) {
        return <TrendingDown width={16} height={16} />;
    }

    return <Minus width={16} height={16} />;
};

function getActiveUsersLabel(activeUsers: number): string {
    if (activeUsers > 150) {
        return 'Strong';
    }

    if (activeUsers > 80) {
        return 'Good';
    }

    return 'Low';
}

function getScoreStyle(score: number): ScoreStyle {
    if (score >= 75) {
        return {
            stroke: '#34d399',
            label: 'Healthy',
            pillClass: 'scc-pill-emerald'
        };
    }

    if (score >= 55) {
        return {
            stroke: '#fbbf24',
            label: 'Fair',
            pillClass: 'scc-pill-amber'
        };
    }

    return {
        stroke: '#f87171',
        label: 'At Risk',
        pillClass: 'scc-pill-red'
    };
}

const SystemHealthPanel = ({ data }: SystemHealthPanelProps) => {
    const renewalStyle = getHealthColor(data.renewalRate, 'renewal');
    const growthStyle = getHealthColor(data.monthlyGrowth, 'growth');

    const activeUsersPercent = Math.min(Math.round((data.activeUsers / 200) * 100), 100);
    const activeUsersLabel = getActiveUsersLabel(data.activeUsers);

    const monthlyGrowthPercent = Math.max(
        0,
        Math.min(Math.round(((data.monthlyGrowth + 20) / 40) * 100), 100)
    );

    const overallScore = Math.round(
        (data.renewalRate * 0.5)
        + ((Math.min(data.activeUsers, 200) / 200) * 100 * 0.3)
        + ((Math.max(0, Math.min(data.monthlyGrowth, 20)) / 20) * 100 * 0.2)
    );

    const scoreStyle = getScoreStyle(overallScore);
    const circumference = 2 * Math.PI * 40;

    return (
        <section className='scc-section scc-card'>
            <h2 className='scc-sectionTitle'>
                <span className='scc-sectionAccent scc-accent-pink' />
                System Health
            </h2>

            <div className='scc-healthScoreCard'>
                <div>
                    <p className='scc-healthScoreLabel'>Overall System Score</p>
                    <p className='scc-healthScoreValue'>{overallScore}</p>
                    <span className={cn('scc-pill', scoreStyle.pillClass)}>
                        {scoreStyle.label}
                    </span>
                </div>

                <div className='scc-gaugeWrap' aria-hidden='true'>
                    <svg viewBox='0 0 100 100' className='scc-gaugeSvg'>
                        <circle cx='50' cy='50' r='40' fill='none' stroke='rgba(255,255,255,0.05)' strokeWidth='10' />
                        <circle
                            cx='50'
                            cy='50'
                            r='40'
                            fill='none'
                            stroke={scoreStyle.stroke}
                            strokeWidth='10'
                            strokeLinecap='round'
                            strokeDasharray={`${circumference}`}
                            strokeDashoffset={`${circumference * (1 - (overallScore / 100))}`}
                            className='scc-gaugeProgress'
                        />
                    </svg>
                    <div className='scc-gaugeIcon'>
                        <Activity width={24} height={24} />
                    </div>
                </div>
            </div>

            <div className='scc-healthGrid'>
                <div className={cn('scc-healthMetric', renewalStyle.bg, renewalStyle.ring)}>
                    <div className='scc-healthMetricTop'>
                        <span className={cn('scc-iconBadge', renewalStyle.bg, renewalStyle.ring, renewalStyle.text)}>
                            <Activity width={16} height={16} />
                        </span>
                        <span className='scc-pill scc-pill-subtle'>{renewalStyle.label}</span>
                    </div>
                    <p className={cn('scc-healthMetricValue', renewalStyle.text)}>{data.renewalRate}%</p>
                    <p className='scc-healthMetricLabel'>Renewal Rate</p>
                    <div className='scc-healthMetricTrack'>
                        <div className={cn('scc-healthMetricFill', renewalStyle.bar)} style={{ width: `${data.renewalRate}%` }} />
                    </div>
                    <p className='scc-healthMetricDesc'>Monthly subscription renewals</p>
                </div>

                <div className='scc-healthMetric scc-healthBg-sky scc-healthRing-sky'>
                    <div className='scc-healthMetricTop'>
                        <span className='scc-iconBadge scc-healthBg-sky scc-healthRing-sky scc-text-sky'>
                            <Users width={16} height={16} />
                        </span>
                        <span className='scc-pill scc-pill-subtle'>{activeUsersLabel}</span>
                    </div>
                    <p className='scc-healthMetricValue scc-text-sky'>{data.activeUsers}</p>
                    <p className='scc-healthMetricLabel'>Active Users</p>
                    <div className='scc-healthMetricTrack'>
                        <div className='scc-healthMetricFill scc-healthBar-sky' style={{ width: `${activeUsersPercent}%` }} />
                    </div>
                    <p className='scc-healthMetricDesc'>Currently active subscriptions</p>
                </div>

                <div className={cn('scc-healthMetric', growthStyle.bg, growthStyle.ring)}>
                    <div className='scc-healthMetricTop'>
                        <span className={cn('scc-iconBadge', growthStyle.bg, growthStyle.ring, growthStyle.text)}>
                            <GrowthIcon value={data.monthlyGrowth} />
                        </span>
                        <span className='scc-pill scc-pill-subtle'>{growthStyle.label}</span>
                    </div>
                    <p className={cn('scc-healthMetricValue', growthStyle.text)}>
                        {data.monthlyGrowth > 0 ? '+' : ''}
                        {data.monthlyGrowth}%
                    </p>
                    <p className='scc-healthMetricLabel'>Monthly Growth</p>
                    <div className='scc-healthMetricTrack'>
                        <div className={cn('scc-healthMetricFill', growthStyle.bar)} style={{ width: `${monthlyGrowthPercent}%` }} />
                    </div>
                    <p className='scc-healthMetricDesc'>Month-over-month user growth</p>
                </div>
            </div>
        </section>
    );
};

export default SystemHealthPanel;
