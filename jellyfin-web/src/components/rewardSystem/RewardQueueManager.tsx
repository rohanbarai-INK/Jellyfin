import { useEffect } from 'react';

import { playbackManager } from 'components/playback/playbackmanager';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import Events from 'utils/events';

import {
    mountAchievementOverlay,
    showAchievement,
    syncAchievementsAndShow,
    unlockAchievementAndShow
} from './AchievementOverlayMount';
import { addActivityRewardHistoryEntry } from './activityRewardHistoryStore';
import { REWARD_SYSTEM_SHOW_EVENT, RewardSystem, type ActiveReward, type RewardPayload } from './RewardSystem';

function syncPlaybackState() {
    RewardSystem.setPlaybackState(playbackManager.isPlaying());
}

type RewardShowEvent = Event & {
    detail: ActiveReward;
};

const RewardQueueManager = () => {
    useEffect(() => {
        return mountAchievementOverlay();
    }, []);

    useEffect(() => {
        let disposed = false;
        let inFlight = false;
        let queued = false;
        let syncTimer: number | null = null;

        const clearSyncTimer = () => {
            if (syncTimer != null) {
                window.clearTimeout(syncTimer);
                syncTimer = null;
            }
        };

        const runSync = async () => {
            if (disposed) {
                return;
            }

            if (inFlight) {
                queued = true;
                return;
            }

            inFlight = true;
            try {
                await syncAchievementsAndShow();
            } catch (error) {
                console.warn('[RewardQueueManager] achievement sync failed', error);
            } finally {
                inFlight = false;
                if (queued && !disposed) {
                    queued = false;
                    scheduleSync(800);
                }
            }
        };

        const scheduleSync = (delayMs = 0) => {
            clearSyncTimer();
            syncTimer = window.setTimeout(() => {
                void runSync();
            }, Math.max(0, delayMs));
        };

        const handlePlaybackStateEvent = () => {
            syncPlaybackState();
            if (!playbackManager.isPlaying()) {
                scheduleSync(1200);
            }
        };

        const handleVisibilityChange = () => {
            if (document.visibilityState === 'visible') {
                scheduleSync(1500);
            }
        };

        syncPlaybackState();
        scheduleSync(1500);

        Events.on(playbackManager, 'playbackstart', handlePlaybackStateEvent);
        Events.on(playbackManager, 'playbackstop', handlePlaybackStateEvent);
        Events.on(playbackManager, 'playbackcancelled', handlePlaybackStateEvent);
        Events.on(playbackManager, 'playerchange', handlePlaybackStateEvent);
        document.addEventListener('visibilitychange', handleVisibilityChange);

        return () => {
            disposed = true;
            clearSyncTimer();
            Events.off(playbackManager, 'playbackstart', handlePlaybackStateEvent);
            Events.off(playbackManager, 'playbackstop', handlePlaybackStateEvent);
            Events.off(playbackManager, 'playbackcancelled', handlePlaybackStateEvent);
            Events.off(playbackManager, 'playerchange', handlePlaybackStateEvent);
            document.removeEventListener('visibilitychange', handleVisibilityChange);
        };
    }, []);

    useEffect(() => {
        window.showAchievement = showAchievement;
        window.unlockAchievement = unlockAchievementAndShow;
        window.syncAchievements = syncAchievementsAndShow;

        return () => {
            if (window.showAchievement === showAchievement) {
                delete window.showAchievement;
            }

            if (window.unlockAchievement === unlockAchievementAndShow) {
                delete window.unlockAchievement;
            }

            if (window.syncAchievements === syncAchievementsAndShow) {
                delete window.syncAchievements;
            }
        };
    }, []);

    useEffect(() => {
        if (!__WEBPACK_SERVE__) {
            return;
        }

        const triggerRewardTest = (payload: RewardPayload) => {
            RewardSystem.enqueue(payload);
        };

        window.__triggerRewardTest = triggerRewardTest;

        return () => {
            if (window.__triggerRewardTest === triggerRewardTest) {
                delete window.__triggerRewardTest;
            }
        };
    }, []);

    useEffect(() => {
        if (typeof window === 'undefined') {
            return () => undefined;
        }

        const seenRewardIds = new Set<number>();

        const handleRewardShowEvent = (event: Event) => {
            const reward = (event as RewardShowEvent).detail;
            if (!reward || reward.type !== 'activity' || seenRewardIds.has(reward.id)) {
                return;
            }

            seenRewardIds.add(reward.id);
            addActivityRewardHistoryEntry({
                rewardId: reward.id,
                userId: ServerConnections.currentApiClient()?.getCurrentUserId(),
                xp: reward.payload.xpEarned,
                coins: reward.payload.coinsEarned
            });
        };

        window.addEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShowEvent);

        return () => {
            window.removeEventListener(REWARD_SYSTEM_SHOW_EVENT, handleRewardShowEvent);
        };
    }, []);

    return null;
};

export default RewardQueueManager;
