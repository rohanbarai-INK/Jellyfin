const ACTIVITY_AGGREGATION_WINDOW_MS = 2000;
const ACTIVITY_AUTOCOMPLETE_MS = 1800;
const ACHIEVEMENT_AUTOCOMPLETE_MS = 5600;

export const REWARD_OVERLAY_ROOT_ID = 'reward-overlay-root';
export const ACHIEVEMENT_OVERLAY_ROOT_ID = 'achievement-overlay-root';

export const REWARD_SYSTEM_SHOW_EVENT = 'reward-system:show';
export const REWARD_SYSTEM_HIDE_EVENT = 'reward-system:hide';
export const REWARD_SYSTEM_COMPLETE_EVENT = 'reward-system:complete';
export const REWARD_SYSTEM_STATE_EVENT = 'reward-system:state';

type RewardQueueType = 'activity' | 'achievement';

export interface RewardPayload {
    xpEarned?: number;
    coinsEarned?: number;
    achievement?: {
        title: string;
        description: string;
        id?: string;
        imageEmoji?: string;
        emoji?: string;
        imageUrl?: string;
        rarity?: 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';
        progress?: number;
        duration?: number;
        historyDisabled?: boolean;
    };
}

export interface ActiveReward {
    id: number;
    type: RewardQueueType;
    payload: RewardPayload;
    containerId: typeof REWARD_OVERLAY_ROOT_ID | typeof ACHIEVEMENT_OVERLAY_ROOT_ID;
}

export interface RewardSystemState {
    activeReward: ActiveReward | null;
    queuedActivityCount: number;
    queuedAchievementCount: number;
    isPlaybackActive: boolean;
    hasPendingActivityAggregate: boolean;
}

interface RewardQueueItem extends ActiveReward {
    autoCompleteMs: number;
}

type RewardSystemListener = (state: RewardSystemState) => void;

export interface RewardSystemPublicApi {
    enqueue(payload: RewardPayload): void;
    setPlaybackState(isPlaying: boolean): void;
    completeActiveReward(id?: number): void;
    getState(): RewardSystemState;
    subscribe(listener: RewardSystemListener): () => void;
}

function sanitizeNumericValue(value: number | undefined): number {
    return Number.isFinite(value) ? Number(value) : 0;
}

function sanitizeAchievement(achievement: RewardPayload['achievement']): RewardPayload['achievement'] | undefined {
    if (!achievement) {
        return undefined;
    }

    const title = achievement.title?.trim();
    const description = achievement.description?.trim();
    if (!title || !description) {
        return undefined;
    }

    const sanitizedAchievement: RewardPayload['achievement'] = {
        title,
        description
    };

    if (achievement.id?.trim()) {
        sanitizedAchievement.id = achievement.id.trim();
    }

    if (achievement.imageEmoji?.trim()) {
        sanitizedAchievement.imageEmoji = achievement.imageEmoji.trim();
    }

    if (achievement.emoji?.trim()) {
        sanitizedAchievement.emoji = achievement.emoji.trim();
    }

    if (achievement.imageUrl?.trim()) {
        sanitizedAchievement.imageUrl = achievement.imageUrl.trim();
    }

    if (achievement.rarity === 'common'
        || achievement.rarity === 'uncommon'
        || achievement.rarity === 'rare'
        || achievement.rarity === 'epic'
        || achievement.rarity === 'legendary') {
        sanitizedAchievement.rarity = achievement.rarity;
    }

    if (Number.isFinite(achievement.progress)) {
        sanitizedAchievement.progress = Math.max(0, Math.min(100, Number(achievement.progress)));
    }

    if (Number.isFinite(achievement.duration)) {
        sanitizedAchievement.duration = Math.max(1200, Number(achievement.duration));
    }

    if (achievement.historyDisabled === true) {
        sanitizedAchievement.historyDisabled = true;
    }

    return sanitizedAchievement;
}

function sanitizePayload(payload: RewardPayload): RewardPayload | null {
    const xpEarned = sanitizeNumericValue(payload.xpEarned);
    const coinsEarned = sanitizeNumericValue(payload.coinsEarned);
    const achievement = sanitizeAchievement(payload.achievement);

    const sanitized: RewardPayload = {};

    if (xpEarned !== 0) {
        sanitized.xpEarned = xpEarned;
    }
    if (coinsEarned !== 0) {
        sanitized.coinsEarned = coinsEarned;
    }
    if (achievement) {
        sanitized.achievement = achievement;
    }

    if (!sanitized.achievement && sanitized.xpEarned == null && sanitized.coinsEarned == null) {
        return null;
    }

    return sanitized;
}

function getContainerId(type: RewardQueueType): typeof REWARD_OVERLAY_ROOT_ID | typeof ACHIEVEMENT_OVERLAY_ROOT_ID {
    return type === 'achievement' ? ACHIEVEMENT_OVERLAY_ROOT_ID : REWARD_OVERLAY_ROOT_ID;
}

function clonePayload(payload: RewardPayload): RewardPayload {
    return {
        xpEarned: payload.xpEarned,
        coinsEarned: payload.coinsEarned,
        achievement: payload.achievement ? {
            title: payload.achievement.title,
            description: payload.achievement.description,
            id: payload.achievement.id,
            imageEmoji: payload.achievement.imageEmoji,
            emoji: payload.achievement.emoji,
            imageUrl: payload.achievement.imageUrl,
            rarity: payload.achievement.rarity,
            progress: payload.achievement.progress,
            duration: payload.achievement.duration,
            historyDisabled: payload.achievement.historyDisabled
        } : undefined
    };
}

class RewardSystemController implements RewardSystemPublicApi {
    private activityQueue: RewardQueueItem[] = [];
    private achievementQueue: RewardQueueItem[] = [];
    private listeners = new Set<RewardSystemListener>();
    private activeReward: RewardQueueItem | null = null;
    private isPlaybackActive = false;
    private nextRewardId = 1;
    private pendingActivity: RewardPayload | null = null;
    private pendingActivityTimer: number | null = null;
    private activeRewardTimer: number | null = null;

    public enqueue(payload: RewardPayload): void {
        const sanitizedPayload = sanitizePayload(payload);
        if (!sanitizedPayload) {
            return;
        }

        if (sanitizedPayload.achievement) {
            this.achievementQueue.push(this.createQueueItem('achievement', {
                achievement: sanitizedPayload.achievement,
                xpEarned: sanitizedPayload.xpEarned,
                coinsEarned: sanitizedPayload.coinsEarned
            }));
        }

        if (!sanitizedPayload.achievement && (sanitizedPayload.xpEarned != null || sanitizedPayload.coinsEarned != null)) {
            this.enqueueActivityAggregate(sanitizedPayload);
        }

        this.emitState();
        this.processQueue();
    }

    public setPlaybackState(isPlaying: boolean): void {
        const playbackActive = Boolean(isPlaying);
        if (this.isPlaybackActive === playbackActive) {
            return;
        }

        this.isPlaybackActive = playbackActive;

        if (playbackActive) {
            this.pauseActiveReward();
        } else {
            this.processQueue();
        }

        this.emitState();
    }

    public completeActiveReward(id?: number): void {
        if (!this.activeReward) {
            return;
        }

        if (id != null && id !== this.activeReward.id) {
            return;
        }

        const completedReward = this.activeReward;
        this.activeReward = null;
        this.clearActiveRewardTimer();
        this.dispatchWindowEvent(REWARD_SYSTEM_HIDE_EVENT, completedReward);
        this.dispatchWindowEvent(REWARD_SYSTEM_COMPLETE_EVENT, completedReward);
        this.emitState();
        this.processQueue();
    }

    public getState(): RewardSystemState {
        return {
            activeReward: this.activeReward ? this.toActiveReward(this.activeReward) : null,
            queuedActivityCount: this.activityQueue.length,
            queuedAchievementCount: this.achievementQueue.length,
            isPlaybackActive: this.isPlaybackActive,
            hasPendingActivityAggregate: Boolean(this.pendingActivity)
        };
    }

    public subscribe(listener: RewardSystemListener): () => void {
        this.listeners.add(listener);
        listener(this.getState());

        return () => {
            this.listeners.delete(listener);
        };
    }

    private createQueueItem(type: RewardQueueType, payload: RewardPayload): RewardQueueItem {
        const isAchievement = type === 'achievement';
        const duration = payload.achievement?.duration;
        const achievementAutoCompleteMs = Number.isFinite(duration) ? Math.max(Number(duration) + 700, 1900) : ACHIEVEMENT_AUTOCOMPLETE_MS;

        return {
            id: this.nextRewardId++,
            type,
            payload: clonePayload(payload),
            containerId: getContainerId(type),
            autoCompleteMs: isAchievement ? achievementAutoCompleteMs : ACTIVITY_AUTOCOMPLETE_MS
        };
    }

    private enqueueActivityAggregate(payload: RewardPayload): void {
        const xpEarned = sanitizeNumericValue(payload.xpEarned);
        const coinsEarned = sanitizeNumericValue(payload.coinsEarned);

        if (xpEarned === 0 && coinsEarned === 0) {
            return;
        }

        if (!this.pendingActivity) {
            this.pendingActivity = {
                xpEarned: 0,
                coinsEarned: 0
            };
        }

        this.pendingActivity.xpEarned = (this.pendingActivity.xpEarned || 0) + xpEarned;
        this.pendingActivity.coinsEarned = (this.pendingActivity.coinsEarned || 0) + coinsEarned;

        if (this.pendingActivityTimer == null) {
            this.pendingActivityTimer = window.setTimeout(() => {
                this.flushPendingActivity();
            }, ACTIVITY_AGGREGATION_WINDOW_MS);
        }
    }

    private flushPendingActivity(): void {
        this.clearPendingActivityTimer();

        const pendingActivity = this.pendingActivity;
        this.pendingActivity = null;

        if (!pendingActivity) {
            this.emitState();
            return;
        }

        const payload = sanitizePayload(pendingActivity);
        if (!payload || (payload.xpEarned == null && payload.coinsEarned == null)) {
            this.emitState();
            return;
        }

        this.activityQueue.push(this.createQueueItem('activity', {
            xpEarned: payload.xpEarned,
            coinsEarned: payload.coinsEarned
        }));

        this.emitState();
        this.processQueue();
    }

    private pauseActiveReward(): void {
        if (!this.activeReward) {
            return;
        }

        const pausedReward = this.activeReward;
        this.activeReward = null;
        this.clearActiveRewardTimer();
        this.dispatchWindowEvent(REWARD_SYSTEM_HIDE_EVENT, pausedReward);

        if (pausedReward.type === 'achievement') {
            this.achievementQueue.unshift(pausedReward);
        } else {
            this.activityQueue.unshift(pausedReward);
        }
    }

    private processQueue(): void {
        if (this.isPlaybackActive || this.activeReward) {
            return;
        }

        const nextReward = this.achievementQueue.shift() || this.activityQueue.shift();
        if (!nextReward) {
            return;
        }

        this.activeReward = nextReward;
        this.dispatchWindowEvent(REWARD_SYSTEM_SHOW_EVENT, nextReward);
        this.emitState();
        this.activeRewardTimer = window.setTimeout(() => {
            this.completeActiveReward(nextReward.id);
        }, nextReward.autoCompleteMs);
    }

    private toActiveReward(reward: RewardQueueItem): ActiveReward {
        return {
            id: reward.id,
            type: reward.type,
            payload: clonePayload(reward.payload),
            containerId: reward.containerId
        };
    }

    private emitState(): void {
        const state = this.getState();

        this.listeners.forEach(listener => {
            listener(state);
        });

        this.dispatchWindowEvent(REWARD_SYSTEM_STATE_EVENT, state);
    }

    private dispatchWindowEvent(eventName: string, detail: ActiveReward | RewardSystemState): void {
        if (typeof window === 'undefined' || typeof window.dispatchEvent !== 'function' || typeof CustomEvent !== 'function') {
            return;
        }

        window.dispatchEvent(new CustomEvent(eventName, {
            detail
        }));
    }

    private clearActiveRewardTimer(): void {
        if (this.activeRewardTimer != null) {
            window.clearTimeout(this.activeRewardTimer);
            this.activeRewardTimer = null;
        }
    }

    private clearPendingActivityTimer(): void {
        if (this.pendingActivityTimer != null) {
            window.clearTimeout(this.pendingActivityTimer);
            this.pendingActivityTimer = null;
        }
    }
}

export const RewardSystem: RewardSystemPublicApi = new RewardSystemController();

if (typeof window !== 'undefined') {
    window.RewardSystem = RewardSystem;
}
