import React, { useEffect, useMemo, useRef } from 'react';

import { getRankForLevel } from 'utils/levelRewards';

interface RankBadgeProps {
    level: number;
}

interface RankTheme {
    primaryRgb: string;
    glowRgb: string;
    accentRgb: string;
    textColor: string;
}

const RANK_THEMES: Record<number, RankTheme> = {
    1: { primaryRgb: '146, 160, 178', glowRgb: '170, 188, 210', accentRgb: '76, 86, 100', textColor: 'rgba(235, 241, 249, 0.96)' },
    2: { primaryRgb: '132, 188, 219', glowRgb: '105, 197, 244', accentRgb: '36, 88, 118', textColor: 'rgba(230, 248, 255, 0.98)' },
    3: { primaryRgb: '162, 210, 228', glowRgb: '84, 210, 236', accentRgb: '32, 113, 126', textColor: 'rgba(235, 250, 255, 0.98)' },
    4: { primaryRgb: '137, 229, 239', glowRgb: '36, 211, 236', accentRgb: '20, 117, 132', textColor: 'rgba(232, 251, 255, 0.99)' },
    5: { primaryRgb: '118, 235, 191', glowRgb: '52, 211, 153', accentRgb: '26, 122, 95', textColor: 'rgba(233, 255, 244, 0.99)' },
    6: { primaryRgb: '136, 224, 226', glowRgb: '94, 234, 212', accentRgb: '28, 126, 130', textColor: 'rgba(235, 255, 255, 0.99)' },
    7: { primaryRgb: '212, 188, 255', glowRgb: '188, 136, 255', accentRgb: '84, 57, 130', textColor: 'rgba(245, 238, 255, 0.99)' },
    8: { primaryRgb: '117, 242, 201', glowRgb: '56, 229, 176', accentRgb: '27, 133, 102', textColor: 'rgba(236, 255, 248, 0.99)' },
    9: { primaryRgb: '230, 238, 255', glowRgb: '174, 199, 255', accentRgb: '102, 126, 189', textColor: 'rgba(245, 249, 255, 0.99)' },
    10: { primaryRgb: '255, 214, 122', glowRgb: '255, 197, 72', accentRgb: '177, 126, 22', textColor: 'rgba(255, 247, 218, 0.99)' }
};

const RankBadge = ({ level }: RankBadgeProps) => {
    const badgeRef = useRef<HTMLDivElement | null>(null);
    const rankTier = useMemo(() => getRankForLevel(level), [level]);
    const rank = rankTier.rank;
    const theme = RANK_THEMES[rank] || RANK_THEMES[1];

    useEffect(() => {
        const badge = badgeRef.current;
        if (!badge) {
            return;
        }

        badge.style.setProperty('--rank-color-rgb', theme.primaryRgb);
        badge.style.setProperty('--rank-glow-rgb', theme.glowRgb);
        badge.style.setProperty('--rank-accent-rgb', theme.accentRgb);
        badge.style.setProperty('--rank-text-color', theme.textColor);

        // requestAnimationFrame updates CSS variables only (no layout reads).
        let frameHandle = 0;
        const baseStrength = rank / 10;
        const amplitude = rank >= 9 ? 0.22 : rank >= 7 ? 0.18 : rank >= 4 ? 0.13 : 0.09;
        const speed = rank === 10 ? 0.00047 : rank >= 7 ? 0.00063 : 0.0008;

        const animate = (timestamp: number) => {
            const wave = (Math.sin(timestamp * speed) + 1) / 2;
            const strength = Math.min(1, baseStrength * (1 - (amplitude / 2) + (amplitude * wave)));
            const auraOpacity = 0.17 + (strength * 0.58);
            const scale = rank >= 4
                ? 1 + ((0.006 + (strength * 0.01)) * wave)
                : 1 + (0.0035 * wave);

            badge.style.setProperty('--glow-strength', strength.toFixed(4));
            badge.style.setProperty('--aura-opacity', auraOpacity.toFixed(4));
            badge.style.setProperty('--badge-scale', scale.toFixed(4));

            frameHandle = window.requestAnimationFrame(animate);
        };

        frameHandle = window.requestAnimationFrame(animate);

        return () => {
            window.cancelAnimationFrame(frameHandle);
        };
    }, [rank, theme.accentRgb, theme.glowRgb, theme.primaryRgb, theme.textColor]);

    return (
        <div
            ref={badgeRef}
            className={`achievementsHeaderRank rankBadge rankBadge-r${rank}${rank === 10 ? ' rankBadge-legend' : ''}`}
            aria-label={`RANK: ${rankTier.title}`}
        >
            <span className='rankBadgeAura' aria-hidden='true' />
            <span className='rankBadgeLegendAura' aria-hidden='true' />
            <span className='rankBadgeShimmer' aria-hidden='true' />

            <span className='rankBadgeContent'>
                <span className='rankBadgePrefix'>RANK:</span>
                <span className='rankBadgeEmoji' aria-hidden='true'>{rankTier.emoji}</span>
                <span className='rankBadgeTitle'>{rankTier.title}</span>
            </span>
        </div>
    );
};

export default RankBadge;
