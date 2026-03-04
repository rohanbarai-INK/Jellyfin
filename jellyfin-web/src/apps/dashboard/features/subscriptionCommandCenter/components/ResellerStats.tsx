import { Award, BarChart3, TrendingUp } from 'lucide-react';
import React, { useMemo } from 'react';

import { cn } from '../utils/cn';

interface ResellerStat {
    reseller: string;
    keysGenerated: number;
    keysRedeemed: number;
    revenue: number;
}

interface ResellerStatsProps {
    data: ResellerStat[];
}

const AVATAR_GRADIENTS = [
    'linear-gradient(135deg, #8b5cf6, #7c3aed)',
    'linear-gradient(135deg, #0ea5e9, #2563eb)',
    'linear-gradient(135deg, #10b981, #14b8a6)',
    'linear-gradient(135deg, #f59e0b, #f97316)',
    'linear-gradient(135deg, #ec4899, #f43f5e)'
];

const getConversionClass = (rate: number): string => {
    if (rate >= 75) {
        return 'scc-text-emerald';
    }

    if (rate >= 50) {
        return 'scc-text-amber';
    }

    return 'scc-text-red';
};

const ResellerStats = ({ data }: ResellerStatsProps) => {
    const sorted = useMemo(
        () => [ ...data ].sort((left, right) => right.revenue - left.revenue),
        [ data ]
    );

    const maxRevenue = sorted[0]?.revenue || 1;

    const totals = useMemo(
        () => sorted.reduce((acc, item) => ({
            generated: acc.generated + item.keysGenerated,
            redeemed: acc.redeemed + item.keysRedeemed,
            revenue: acc.revenue + item.revenue
        }), {
            generated: 0,
            redeemed: 0,
            revenue: 0
        }),
        [ sorted ]
    );

    return (
        <section className='scc-section scc-card scc-tableCard'>
            <h2 className='scc-sectionTitle'>
                <span className='scc-sectionAccent scc-accent-emerald' />
                Reseller Performance
            </h2>

            <div className='scc-tableHeader'>
                <BarChart3 width={16} height={16} className='scc-text-emerald' />
                <span>{data.length} active resellers</span>
            </div>

            <div className='scc-tableWrap'>
                <table className='scc-table'>
                    <thead>
                        <tr>
                            <th>#</th>
                            <th>Reseller</th>
                            <th className='scc-right'>Generated</th>
                            <th className='scc-right'>Redeemed</th>
                            <th className='scc-right'>Conv. %</th>
                            <th className='scc-right'>Revenue</th>
                            <th>Share</th>
                        </tr>
                    </thead>
                    <tbody>
                        {sorted.map((item, index) => {
                            const conversionRate = item.keysGenerated > 0 ? Math.round((item.keysRedeemed / item.keysGenerated) * 100) : 0;
                            const share = Math.round((item.revenue / maxRevenue) * 100);

                            return (
                                <tr key={item.reseller}>
                                    <td>
                                        {index === 0 ? (
                                            <Award width={16} height={16} className='scc-text-amber' />
                                        ) : (
                                            <span className='scc-rank'>{index + 1}</span>
                                        )}
                                    </td>
                                    <td>
                                        <div className='scc-resellerCell'>
                                            <div
                                                className='scc-resellerAvatar'
                                                style={{
                                                    background: AVATAR_GRADIENTS[index % AVATAR_GRADIENTS.length]
                                                }}
                                            >
                                                {item.reseller.charAt(0)}
                                            </div>
                                            <span>{item.reseller}</span>
                                        </div>
                                    </td>
                                    <td className='scc-right scc-text-muted scc-tabular'>{item.keysGenerated}</td>
                                    <td className='scc-right scc-text-muted scc-tabular'>{item.keysRedeemed}</td>
                                    <td className={cn('scc-right scc-tabular', getConversionClass(conversionRate))}>
                                        {conversionRate}%
                                    </td>
                                    <td className='scc-right scc-text-emerald scc-tabular scc-strong'>
                                        {'\u20B9'}{item.revenue.toLocaleString('en-IN')}
                                    </td>
                                    <td>
                                        <div className='scc-shareCell'>
                                            <div className='scc-shareTrack'>
                                                <div className='scc-shareFill' style={{ width: `${share}%` }} />
                                            </div>
                                            <span className='scc-shareValue'>{share}%</span>
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>

            <div className='scc-tableFooter'>
                <span className='scc-tableFooterLabel'>
                    <TrendingUp width={14} height={14} />
                    Total across all resellers
                </span>
                <span className='scc-tableFooterValues'>
                    <span>{totals.generated} generated</span>
                    <span>{totals.redeemed} redeemed</span>
                    <span className='scc-text-emerald'>{'\u20B9'}{totals.revenue.toLocaleString('en-IN')}</span>
                </span>
            </div>
        </section>
    );
};

export default ResellerStats;
