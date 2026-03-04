import { Key } from 'lucide-react';
import React, { useMemo } from 'react';
import {
    Cell,
    Pie,
    PieChart,
    ResponsiveContainer,
    Tooltip
} from 'recharts';

import { type KeyStats } from '../data/api';

interface KeyAnalyticsProps {
    data: KeyStats;
}

interface ChartDatum {
    name: 'Redeemed' | 'Unused' | 'Expired';
    value: number;
    pct: number;
}

interface ChartTooltipProps {
    active?: boolean;
    payload?: Array<{
        payload?: ChartDatum;
    }>;
}

const COLORS: Record<ChartDatum['name'], string> = {
    Redeemed: '#8b5cf6',
    Unused: '#38bdf8',
    Expired: '#f87171'
};

const AnalyticsTooltip = ({ active, payload }: ChartTooltipProps) => {
    if (!active || !payload || payload.length === 0) {
        return null;
    }

    const datum = payload[0]?.payload as ChartDatum | undefined;

    if (!datum) {
        return null;
    }

    return (
        <div className='scc-chartTooltip'>
            <p className='scc-chartTooltipTitle'>{datum.name}</p>
            <p className='scc-chartTooltipText'>{datum.value} keys ({datum.pct}%)</p>
        </div>
    );
};

const KeyAnalytics = ({ data }: KeyAnalyticsProps) => {
    const total = data.redeemed + data.unused + data.expired;

    const chartData = useMemo<ChartDatum[]>(() => {
        const safeTotal = Math.max(total, 1);

        return [
            {
                name: 'Redeemed',
                value: data.redeemed,
                pct: Math.round((data.redeemed / safeTotal) * 100)
            },
            {
                name: 'Unused',
                value: data.unused,
                pct: Math.round((data.unused / safeTotal) * 100)
            },
            {
                name: 'Expired',
                value: data.expired,
                pct: Math.round((data.expired / safeTotal) * 100)
            }
        ];
    }, [ data, total ]);

    return (
        <section className='scc-section scc-card'>
            <h2 className='scc-sectionTitle'>
                <span className='scc-sectionAccent scc-accent-violet' />
                Key Analytics
            </h2>

            <div className='scc-keyHeader'>
                <Key width={16} height={16} className='scc-text-violet' />
                <span>Total Generated: {data.totalGenerated}</span>
            </div>

            <div className='scc-keyAnalyticsLayout'>
                <div className='scc-keyChart'>
                    <ResponsiveContainer width='100%' height='100%'>
                        <PieChart>
                            <Pie
                                data={chartData}
                                dataKey='value'
                                nameKey='name'
                                innerRadius={55}
                                outerRadius={80}
                                paddingAngle={3}
                            >
                                {chartData.map((entry) => (
                                    <Cell key={entry.name} fill={COLORS[entry.name]} stroke='transparent' />
                                ))}
                            </Pie>
                            <Tooltip content={<AnalyticsTooltip />} />
                        </PieChart>
                    </ResponsiveContainer>
                </div>

                <div className='scc-keyLegend'>
                    {chartData.map((entry) => (
                        <div key={entry.name} className='scc-keyLegendRow'>
                            <span
                                className='scc-dot'
                                style={{ backgroundColor: COLORS[entry.name] }}
                                aria-hidden='true'
                            />
                            <span className='scc-keyLegendName'>{entry.name}</span>
                            <div className='scc-keyLegendTrack'>
                                <div
                                    className='scc-keyLegendFill'
                                    style={{
                                        width: `${entry.pct}%`,
                                        backgroundColor: COLORS[entry.name]
                                    }}
                                />
                            </div>
                            <span className='scc-keyLegendValue'>{entry.value}</span>
                            <span className='scc-keyLegendPct'>{entry.pct}%</span>
                        </div>
                    ))}
                </div>
            </div>
        </section>
    );
};

export default KeyAnalytics;
