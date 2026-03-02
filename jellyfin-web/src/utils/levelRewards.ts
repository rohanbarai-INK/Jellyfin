export const MILESTONE_COIN_REWARDS: Record<number, number> = {
    10: 30,
    20: 50,
    30: 80,
    40: 120,
    50: 180,
    60: 260,
    70: 360,
    80: 500,
    90: 700,
    100: 1000
};

export const TOTAL_LIFETIME_MILESTONE_COINS = Object.values(MILESTONE_COIN_REWARDS)
    .reduce((sum, amount) => sum + amount, 0);

const XP_CURVE_BASE = 35;
const XP_CURVE_EXPONENT = 1.2;

export interface RankTier {
    rank: number;
    minLevel: number;
    maxLevel: number;
    title: string;
    emoji: string;
}

const RANK_TIERS: RankTier[] = [
    { rank: 1, minLevel: 1, maxLevel: 10, title: 'Viewer', emoji: '\uD83D\uDC41\uFE0F' },
    { rank: 2, minLevel: 11, maxLevel: 20, title: 'Explorer', emoji: '\uD83E\uDDED' },
    { rank: 3, minLevel: 21, maxLevel: 30, title: 'Enthusiast', emoji: '\uD83C\uDFAC' },
    { rank: 4, minLevel: 31, maxLevel: 40, title: 'Streamer', emoji: '\uD83D\uDCE1' },
    { rank: 5, minLevel: 41, maxLevel: 50, title: 'Curator', emoji: '\uD83D\uDDC2\uFE0F' },
    { rank: 6, minLevel: 51, maxLevel: 60, title: 'Collector', emoji: '\uD83D\uDCC0' },
    { rank: 7, minLevel: 61, maxLevel: 70, title: 'Connoisseur', emoji: '\uD83C\uDF77' },
    { rank: 8, minLevel: 71, maxLevel: 80, title: 'Elite', emoji: '\uD83D\uDEE1\uFE0F' },
    { rank: 9, minLevel: 81, maxLevel: 90, title: 'Master', emoji: '\uD83C\uDFC6' },
    { rank: 10, minLevel: 91, maxLevel: 100, title: 'Legend', emoji: '\uD83D\uDC51' }
];

function normalizeNonNegativeInteger(value: number): number {
    if (!Number.isFinite(value)) {
        return 0;
    }

    return Math.max(0, Math.floor(value));
}

export function getXpRequiredForLevel(level: number): number {
    const normalizedLevel = normalizeNonNegativeInteger(level);
    if (normalizedLevel <= 0) {
        return 0;
    }

    return Math.max(1, Math.round(XP_CURVE_BASE * Math.pow(normalizedLevel, XP_CURVE_EXPONENT)));
}

export function getTotalXpForLevel(level: number): number {
    const normalizedLevel = normalizeNonNegativeInteger(level);
    let totalXp = 0;

    for (let currentLevel = 1; currentLevel <= normalizedLevel; currentLevel++) {
        totalXp += getXpRequiredForLevel(currentLevel);
    }

    return totalXp;
}

export function getLevelForTotalXp(totalXp: number): number {
    const normalizedXp = normalizeNonNegativeInteger(totalXp);
    let level = 0;
    let spentXp = 0;

    while (true) {
        const nextLevel = level + 1;
        const xpRequiredForNext = getXpRequiredForLevel(nextLevel);
        if (spentXp + xpRequiredForNext > normalizedXp) {
            break;
        }

        spentXp += xpRequiredForNext;
        level = nextLevel;
    }

    return level;
}

export function getRankForLevel(level: number): RankTier {
    const normalizedLevel = Math.max(1, Math.min(100, Math.floor(level)));
    const rank = RANK_TIERS.find(({ minLevel, maxLevel }) => normalizedLevel >= minLevel && normalizedLevel <= maxLevel);

    return rank || RANK_TIERS[0];
}

export function getRankIndexForLevel(level: number): number {
    return getRankForLevel(level).rank;
}

export function getNextMilestoneLevel(currentLevel: number): number {
    const normalizedLevel = Math.max(0, Math.floor(currentLevel));
    const nextMilestone = Math.ceil((normalizedLevel + 1) / 10) * 10;
    return Math.max(10, Math.min(100, nextMilestone));
}

export function getMilestoneReward(level: number): number {
    const normalizedLevel = Math.max(0, Math.floor(level));
    return MILESTONE_COIN_REWARDS[normalizedLevel] || 0;
}

export function getLifetimeMilestoneCoins(level: number): number {
    const normalizedLevel = Math.max(0, Math.floor(level));

    return Object.entries(MILESTONE_COIN_REWARDS)
        .reduce((sum, [ milestone, amount ]) => {
            if (Number(milestone) <= normalizedLevel) {
                return sum + amount;
            }

            return sum;
        }, 0);
}
