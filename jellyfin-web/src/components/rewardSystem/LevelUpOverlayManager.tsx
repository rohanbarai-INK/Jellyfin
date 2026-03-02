import React, { useCallback, useEffect, useRef, useState } from 'react';

import { useApi } from 'hooks/useApi';
import { getLevelQuote } from 'utils/quotes';
import { getLevelForTotalXp, getMilestoneReward } from 'utils/levelRewards';

import { getAchievementHistory, subscribeAchievementHistory } from './achievementHistoryStore';
import { getActivityRewardTotals, subscribeActivityRewardHistory } from './activityRewardHistoryStore';
import { triggerCoinRewardOverlay } from './CoinRewardOverlay';
import LevelUpOverlay from './LevelUpOverlay';
import { REWARD_SYSTEM_SHOW_EVENT, type ActiveReward } from './RewardSystem';

interface QueuedLevelUp {
    previousLevel: number;
    level: number;
}

type RewardShowEvent = Event & {
    detail: ActiveReward;
};

const LevelUpOverlayManager = () => {
    const { user: currentUser } = useApi();
    const userId = currentUser?.Id || '';

    const [activeLevelUp, setActiveLevelUp] = useState<QueuedLevelUp | null>(null);
    const totalXpRef = useRef(0);
    const currentLevelRef = useRef(0);
    const queuedLevelUpsRef = useRef<QueuedLevelUp[]>([]);
    const activeLevelUpRef = useRef<QueuedLevelUp | null>(null);
    const processedRewardIdsRef = useRef<Set<number>>(new Set());

    const openNextLevelUp = useCallback(() => {
        if (activeLevelUpRef.current) {
            return;
        }

        const nextLevelUp = queuedLevelUpsRef.current.shift();
        if (!nextLevelUp) {
            return;
        }

        activeLevelUpRef.current = nextLevelUp;
        setActiveLevelUp(nextLevelUp);
    }, []);

    const closeActiveLevelUp = useCallback(() => {
        activeLevelUpRef.current = null;
        setActiveLevelUp(null);
        window.setTimeout(() => {
            openNextLevelUp();
        }, 0);
    }, [openNextLevelUp]);

    const handleClaim = useCallback(() => {
        const active = activeLevelUpRef.current;
        if (active && active.level % 10 === 0) {
            const milestoneCoins = getMilestoneReward(active.level);
            if (milestoneCoins > 0) {
                triggerCoinRewardOverlay(milestoneCoins);
            }
        }

        closeActiveLevelUp();
    }, [closeActiveLevelUp]);

    const calculateTotalXp = useCallback(() => {
        if (!userId) {
            return 0;
        }

        const achievementXp = getAchievementHistory(userId).reduce((sum, entry) => {
            return sum + (Number(entry.xp) || 0);
        }, 0);
        const activityXp = getActivityRewardTotals(userId).xp;

        return achievementXp + activityXp;
    }, [userId]);

    const syncBaseline = useCallback(() => {
        if (!userId) {
            totalXpRef.current = 0;
            currentLevelRef.current = 0;
            queuedLevelUpsRef.current = [];
            activeLevelUpRef.current = null;
            setActiveLevelUp(null);
            return;
        }

        const totalXp = calculateTotalXp();
        totalXpRef.current = totalXp;
        currentLevelRef.current = getLevelForTotalXp(totalXp);
    }, [calculateTotalXp, userId]);

    useEffect(() => {
        syncBaseline();

        if (!userId) {
            return () => undefined;
        }

        const unsubscribeAchievementHistory = subscribeAchievementHistory(() => {
            syncBaseline();
        });

        const unsubscribeActivityHistory = subscribeActivityRewardHistory(() => {
            syncBaseline();
        });

        return () => {
            unsubscribeAchievementHistory();
            unsubscribeActivityHistory();
        };
    }, [syncBaseline, userId]);

    useEffect(() => {
        if (typeof window === 'undefined') {
            return () => undefined;
        }

        const handleRewardShow = (event: Event) => {
            if (!userId) {
                return;
            }

            const reward = (event as RewardShowEvent).detail;
            if (!reward) {
                return;
            }

            if (processedRewardIdsRef.current.has(reward.id)) {
                return;
            }

            processedRewardIdsRef.current.add(reward.id);
            if (processedRewardIdsRef.current.size > 4000) {
                processedRewardIdsRef.current.clear();
                processedRewardIdsRef.current.add(reward.id);
            }

            const xpEarned = Number(reward.payload.xpEarned);
            if (!Number.isFinite(xpEarned) || xpEarned <= 0) {
                return;
            }

            const previousLevel = currentLevelRef.current;
            totalXpRef.current += xpEarned;
            const nextLevel = getLevelForTotalXp(totalXpRef.current);
            currentLevelRef.current = nextLevel;

            if (nextLevel <= previousLevel) {
                return;
            }

            for (let level = previousLevel + 1; level <= nextLevel; level++) {
                queuedLevelUpsRef.current.push({
                    previousLevel: level - 1,
                    level
                });
            }

            openNextLevelUp();
        };

        window.addEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShow);

        return () => {
            window.removeEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShow);
        };
    }, [openNextLevelUp, userId]);

    const overlayLevel = activeLevelUp?.level || 0;
    const overlayPreviousLevel = activeLevelUp?.previousLevel || Math.max(0, overlayLevel - 1);
    const overlayRewardCoins = getMilestoneReward(overlayLevel);
    const overlayQuote = getLevelQuote(overlayLevel);

    return (
        <LevelUpOverlay
            isOpen={Boolean(activeLevelUp)}
            level={overlayLevel}
            previousLevel={overlayPreviousLevel}
            rewardCoins={overlayRewardCoins}
            quote={overlayQuote}
            onClaim={handleClaim}
            onDismiss={closeActiveLevelUp}
        />
    );
};

export default LevelUpOverlayManager;
