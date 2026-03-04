import { Users } from 'lucide-react';
import React, { useCallback, useState } from 'react';
import {
    Area,
    AreaChart,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis
} from 'recharts';

import { type CohortData } from '../data/api';
import { cn } from '../utils/cn';

interface CohortChartProps {
    data: CohortData[];
}

type Mode = 'combined' | 'renewal' | 'joined';

interface TooltipDatum {
    name: string;
    value: number;
    color: string;
}

interface ChartTooltipProps {
    active?: boolean;
    payload?: TooltipDatum[];
    label?: string;
}

function getRenewalColor(rate: number): string {
    if (rate >= 75) {
        return '#34d399';
    }

    if (rate >= 60) {
        return '#fbbf24';
    }

    return '#f87171';
}

const ChartTooltip = ({ active, payload, label }: ChartTooltipProps) => {
    if (!active || !payload || payload.length === 0) {
        return null;
    }

    const values = payload as TooltipDatum[];

    return (
        <div className='scc-chartTooltip'>
            <p className='scc-chartTooltipTitle'>{label}</p>
            {values.map((item) => (
                <p key={item.name} className='scc-chartTooltipText'>
                    <span style={{ color: item.color }}>{item.name}</span>:{' '}
                    <span className='scc-text-white'>
                        {item.name === 'Renewal Rate' ? `${item.value}%` : item.value}
                    </span>
                </p>
            ))}
        </div>
    );
};

const CohortChart = ({ data }: CohortChartProps) => {
    const [ mode, setMode ] = useState<Mode>('combined');
    const setModeCombined = useCallback(() => setMode('combined'), []);
    const setModeRenewal = useCallback(() => setMode('renewal'), []);
    const setModeJoined = useCallback(() => setMode('joined'), []);
    const percentTickFormatter = useCallback((value: number | string) => `${value}%`, []);

    return (
        <section className='scc-section scc-card'>
            <div className='scc-sectionHeadRow'>
                <h2 className='scc-sectionTitle'>
                    <span className='scc-sectionAccent scc-accent-teal' />
                    Cohort Renewal Trends
                </h2>

                <div className='scc-toggleGroup' role='group' aria-label='Cohort mode'>
                    <button
                        type='button'
                        className={cn('scc-toggleButton', mode === 'combined' && 'scc-toggleButtonActive')}
                        onClick={setModeCombined}
                    >
                        Combined
                    </button>
                    <button
                        type='button'
                        className={cn('scc-toggleButton', mode === 'renewal' && 'scc-toggleButtonActive')}
                        onClick={setModeRenewal}
                    >
                        Renewal
                    </button>
                    <button
                        type='button'
                        className={cn('scc-toggleButton', mode === 'joined' && 'scc-toggleButtonActive')}
                        onClick={setModeJoined}
                    >
                        Joined
                    </button>
                </div>
            </div>

            <div className='scc-cohortMiniGrid'>
                {data.map((entry) => (
                    <div key={entry.month} className='scc-cohortMiniCard'>
                        <p className='scc-cohortMonth'>{entry.month}</p>
                        <p className='scc-cohortRate' style={{ color: getRenewalColor(entry.renewalRate) }}>
                            {entry.renewalRate}%
                        </p>
                        <p className='scc-cohortJoined'>
                            <Users width={10} height={10} />
                            {entry.usersJoined}
                        </p>
                    </div>
                ))}
            </div>

            <div className='scc-cohortChartWrap'>
                {mode === 'combined' && (
                    <ResponsiveContainer width='100%' height='100%'>
                        <BarChart data={data}>
                            <CartesianGrid strokeDasharray='3 3' stroke='rgba(255,255,255,0.04)' vertical={false} />
                            <XAxis dataKey='month' tick={{ fill: '#64748b', fontSize: 11 }} axisLine={false} tickLine={false} />
                            <YAxis yAxisId='left' tick={{ fill: '#64748b', fontSize: 11 }} axisLine={false} tickLine={false} />
                            <YAxis
                                yAxisId='right'
                                orientation='right'
                                domain={[ 0, 100 ]}
                                tickFormatter={percentTickFormatter}
                                tick={{ fill: '#64748b', fontSize: 11 }}
                                axisLine={false}
                                tickLine={false}
                            />
                            <Tooltip content={<ChartTooltip />} />
                            <Bar yAxisId='left' dataKey='usersJoined' name='Users Joined' fill='#38bdf8' radius={[ 3, 3, 0, 0 ]} opacity={0.8} />
                            <Bar yAxisId='right' dataKey='renewalRate' name='Renewal Rate' radius={[ 3, 3, 0, 0 ]}>
                                {data.map((entry) => (
                                    <Cell key={`${entry.month}-renewal`} fill={getRenewalColor(entry.renewalRate)} />
                                ))}
                            </Bar>
                        </BarChart>
                    </ResponsiveContainer>
                )}

                {mode === 'renewal' && (
                    <ResponsiveContainer width='100%' height='100%'>
                        <AreaChart data={data}>
                            <defs>
                                <linearGradient id='renewalGrad' x1='0' y1='0' x2='0' y2='1'>
                                    <stop offset='0%' stopColor='#14b8a6' stopOpacity={0.3} />
                                    <stop offset='100%' stopColor='#14b8a6' stopOpacity={0} />
                                </linearGradient>
                            </defs>
                            <CartesianGrid strokeDasharray='3 3' stroke='rgba(255,255,255,0.04)' vertical={false} />
                            <XAxis dataKey='month' tick={{ fill: '#64748b', fontSize: 11 }} axisLine={false} tickLine={false} />
                            <YAxis
                                domain={[ 0, 100 ]}
                                tickFormatter={percentTickFormatter}
                                tick={{ fill: '#64748b', fontSize: 11 }}
                                axisLine={false}
                                tickLine={false}
                            />
                            <Tooltip content={<ChartTooltip />} />
                            <Area
                                type='monotone'
                                dataKey='renewalRate'
                                name='Renewal Rate'
                                stroke='#14b8a6'
                                strokeWidth={2}
                                fill='url(#renewalGrad)'
                            />
                        </AreaChart>
                    </ResponsiveContainer>
                )}

                {mode === 'joined' && (
                    <ResponsiveContainer width='100%' height='100%'>
                        <BarChart data={data}>
                            <CartesianGrid strokeDasharray='3 3' stroke='rgba(255,255,255,0.04)' vertical={false} />
                            <XAxis dataKey='month' tick={{ fill: '#64748b', fontSize: 11 }} axisLine={false} tickLine={false} />
                            <YAxis tick={{ fill: '#64748b', fontSize: 11 }} axisLine={false} tickLine={false} />
                            <Tooltip content={<ChartTooltip />} />
                            <Bar dataKey='usersJoined' name='Users Joined' fill='#38bdf8' radius={[ 3, 3, 0, 0 ]} />
                        </BarChart>
                    </ResponsiveContainer>
                )}
            </div>
        </section>
    );
};

export default CohortChart;
