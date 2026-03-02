import React, { useEffect, useRef } from 'react';

import { useProgressiveSystem } from 'hooks/useProgressiveSystem';

import NotificationContainer from './NotificationContainer';
import {
    REWARD_SYSTEM_COMPLETE_EVENT,
    REWARD_SYSTEM_SHOW_EVENT,
    REWARD_SYSTEM_STATE_EVENT,
    RewardSystem,
    type ActiveReward
} from './RewardSystem';

import './coinRewardOverlay.scss';

type RewardShowEvent = Event & {
    detail: ActiveReward;
};

const COIN_REWARD_OVERLAY_TRIGGER_EVENT = 'coin-reward-overlay:trigger';

type CoinRewardOverlayTriggerEvent = CustomEvent<{
    coins?: number;
}>;

export function triggerCoinRewardOverlay(coins: number) {
    const normalizedCoins = Number(coins);
    if (typeof window === 'undefined'
        || typeof window.dispatchEvent !== 'function'
        || typeof CustomEvent !== 'function'
        || !Number.isFinite(normalizedCoins)
        || normalizedCoins <= 0) {
        return;
    }

    window.dispatchEvent(new CustomEvent(COIN_REWARD_OVERLAY_TRIGGER_EVENT, {
        detail: {
            coins: normalizedCoins
        }
    }));
}

const CoinRewardOverlay = () => {
    const {
        notifications,
        addScore,
        handleNotificationComplete
    } = useProgressiveSystem();
    const queuedAchievementCoins = useRef<number[]>([]);
    const flushTimer = useRef<number | null>(null);

    useEffect(() => {
        if (typeof window === 'undefined') {
            return () => undefined;
        }

        const clearFlushTimer = () => {
            if (flushTimer.current != null) {
                window.clearTimeout(flushTimer.current);
                flushTimer.current = null;
            }
        };

        const flushAchievementCoins = () => {
            clearFlushTimer();

            const activeType = RewardSystem.getState().activeReward?.type;
            if (activeType === 'achievement') {
                flushTimer.current = window.setTimeout(flushAchievementCoins, 420);
                return;
            }

            const nextCoins = queuedAchievementCoins.current.shift();
            if (!nextCoins) {
                return;
            }

            addScore(nextCoins);

            if (queuedAchievementCoins.current.length > 0) {
                flushTimer.current = window.setTimeout(flushAchievementCoins, 320);
            }
        };

        const scheduleFlush = (delayMs: number) => {
            clearFlushTimer();
            flushTimer.current = window.setTimeout(flushAchievementCoins, Math.max(0, delayMs));
        };

        const handleRewardShow = (event: Event) => {
            const reward = (event as RewardShowEvent).detail;
            if (!reward || reward.type !== 'activity') {
                return;
            }

            const coinsEarned = Number(reward.payload.coinsEarned);
            if (!Number.isFinite(coinsEarned) || coinsEarned <= 0) {
                return;
            }

            addScore(coinsEarned);
        };

        const handleRewardComplete = (event: Event) => {
            const reward = (event as RewardShowEvent).detail;
            if (!reward || reward.type !== 'achievement') {
                return;
            }

            const coinsEarned = Number(reward.payload.coinsEarned);
            if (!Number.isFinite(coinsEarned) || coinsEarned <= 0) {
                return;
            }

            queuedAchievementCoins.current.push(coinsEarned);
            // Keep achievement toast visually first and avoid overlap with queued achievements.
            scheduleFlush(560);
        };

        const handleRewardState = () => {
            if (queuedAchievementCoins.current.length === 0) {
                return;
            }

            if (RewardSystem.getState().activeReward?.type !== 'achievement') {
                scheduleFlush(120);
            }
        };

        const handleDirectCoinTrigger = (event: Event) => {
            const coinsEarned = Number((event as CoinRewardOverlayTriggerEvent).detail?.coins);
            if (!Number.isFinite(coinsEarned) || coinsEarned <= 0) {
                return;
            }

            addScore(coinsEarned);
        };

        window.addEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShow);
        window.addEventListener(REWARD_SYSTEM_COMPLETE_EVENT, handleRewardComplete);
        window.addEventListener(REWARD_SYSTEM_STATE_EVENT, handleRewardState);
        window.addEventListener(COIN_REWARD_OVERLAY_TRIGGER_EVENT, handleDirectCoinTrigger as EventListener);

        return () => {
            window.removeEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShow);
            window.removeEventListener(REWARD_SYSTEM_COMPLETE_EVENT, handleRewardComplete);
            window.removeEventListener(REWARD_SYSTEM_STATE_EVENT, handleRewardState);
            window.removeEventListener(COIN_REWARD_OVERLAY_TRIGGER_EVENT, handleDirectCoinTrigger as EventListener);
            clearFlushTimer();
        };
    }, [ addScore ]);

    return (
        <NotificationContainer
            items={notifications}
            onComplete={handleNotificationComplete}
        />
    );
};

export default CoinRewardOverlay;
