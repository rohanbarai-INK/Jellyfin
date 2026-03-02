export interface ActivityRewardHistoryEntry {
    id: string;
    rewardId?: number;
    userId?: string;
    xp: number;
    coins: number;
    awardedAt: string;
}

interface NewActivityRewardHistoryEntry {
    rewardId?: number;
    userId?: string;
    xp?: number;
    coins?: number;
    awardedAt?: string;
}

interface ActivityRewardTotals {
    xp: number;
    coins: number;
    count: number;
}

type ActivityRewardHistoryListener = (entries: ActivityRewardHistoryEntry[]) => void;

const STORAGE_KEY = 'jellyfin.reward.activityHistory.v1';
const MAX_HISTORY_ENTRIES = 600;

const listeners = new Set<ActivityRewardHistoryListener>();
let historyEntries: ActivityRewardHistoryEntry[] = [];

function getStorage(): Storage | null {
    if (typeof window === 'undefined') {
        return null;
    }

    try {
        return window.localStorage;
    } catch (error) {
        console.warn('[activityRewardHistoryStore] localStorage unavailable', error);
        return null;
    }
}

function normalizeString(value: unknown): string {
    if (typeof value !== 'string') {
        return '';
    }

    return value.trim();
}

function normalizeNumber(value: unknown): number {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
        return 0;
    }

    return parsed;
}

function createEntryId(): string {
    if (typeof window !== 'undefined' && window.crypto?.getRandomValues) {
        const values = new Uint32Array(2);
        window.crypto.getRandomValues(values);
        return `${Date.now().toString(36)}-${values[0].toString(36)}${values[1].toString(36)}`;
    }

    // eslint-disable-next-line sonarjs/pseudo-random
    return `${Date.now().toString(36)}-${Math.floor(Math.random() * 1000000000).toString(36)}`;
}

function normalizeEntry(value: unknown): ActivityRewardHistoryEntry | null {
    if (!value || typeof value !== 'object') {
        return null;
    }

    const source = value as Record<string, unknown>;
    const awardedAtRaw = normalizeString(source.awardedAt);
    const awardedDate = awardedAtRaw ? new Date(awardedAtRaw) : null;
    const awardedAt = awardedDate && !Number.isNaN(awardedDate.getTime()) ? awardedDate.toISOString() : new Date().toISOString();
    const xp = normalizeNumber(source.xp);
    const coins = normalizeNumber(source.coins);
    const rewardIdRaw = Number(source.rewardId);
    const rewardId = Number.isFinite(rewardIdRaw) ? Math.max(0, Math.trunc(rewardIdRaw)) : undefined;

    if (xp === 0 && coins === 0) {
        return null;
    }

    return {
        id: normalizeString(source.id) || createEntryId(),
        rewardId,
        userId: normalizeString(source.userId) || undefined,
        xp,
        coins,
        awardedAt
    };
}

function persistHistory() {
    const storage = getStorage();
    if (!storage) {
        return;
    }

    try {
        storage.setItem(STORAGE_KEY, JSON.stringify(historyEntries));
    } catch (error) {
        console.warn('[activityRewardHistoryStore] failed to persist history', error);
    }
}

function emitHistoryUpdate() {
    const snapshot = historyEntries.slice();
    listeners.forEach(listener => {
        listener(snapshot);
    });
}

function loadInitialHistory() {
    const storage = getStorage();
    if (!storage) {
        historyEntries = [];
        return;
    }

    try {
        const raw = storage.getItem(STORAGE_KEY);
        if (!raw) {
            historyEntries = [];
            return;
        }

        const parsed = JSON.parse(raw);
        if (!Array.isArray(parsed)) {
            historyEntries = [];
            return;
        }

        historyEntries = parsed
            .map(normalizeEntry)
            .filter((entry): entry is ActivityRewardHistoryEntry => Boolean(entry))
            .sort((a, b) => new Date(b.awardedAt).getTime() - new Date(a.awardedAt).getTime())
            .slice(0, MAX_HISTORY_ENTRIES);
    } catch (error) {
        console.warn('[activityRewardHistoryStore] failed to load history', error);
        historyEntries = [];
    }
}

loadInitialHistory();

export function addActivityRewardHistoryEntry(entry: NewActivityRewardHistoryEntry): ActivityRewardHistoryEntry | null {
    const normalized = normalizeEntry({
        ...entry,
        id: createEntryId(),
        awardedAt: entry.awardedAt || new Date().toISOString()
    });

    if (!normalized) {
        return null;
    }

    historyEntries = [normalized, ...historyEntries]
        .slice(0, MAX_HISTORY_ENTRIES);

    persistHistory();
    emitHistoryUpdate();

    return normalized;
}

export function getActivityRewardHistory(userId?: string): ActivityRewardHistoryEntry[] {
    if (!userId) {
        return historyEntries.slice();
    }

    return historyEntries.filter(entry => entry.userId === userId);
}

export function getActivityRewardTotals(userId?: string): ActivityRewardTotals {
    return getActivityRewardHistory(userId).reduce<ActivityRewardTotals>((totals, entry) => {
        totals.xp += entry.xp;
        totals.coins += entry.coins;
        totals.count++;
        return totals;
    }, {
        xp: 0,
        coins: 0,
        count: 0
    });
}

export function subscribeActivityRewardHistory(listener: ActivityRewardHistoryListener): () => void {
    listeners.add(listener);
    listener(historyEntries.slice());

    return () => {
        listeners.delete(listener);
    };
}

export function clearActivityRewardHistory(userId?: string): void {
    if (!userId) {
        historyEntries = [];
    } else {
        historyEntries = historyEntries.filter(entry => entry.userId !== userId);
    }

    persistHistory();
    emitHistoryUpdate();
}
