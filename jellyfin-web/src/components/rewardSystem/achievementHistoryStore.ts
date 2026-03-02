export type AchievementRarity = 'common' | 'uncommon' | 'rare' | 'epic' | 'legendary';

export interface AchievementHistoryEntry {
    id: string;
    achievementId?: string;
    userId?: string;
    title: string;
    description: string;
    emoji?: string;
    imageUrl?: string;
    rarity: AchievementRarity;
    xp: number;
    coins: number;
    unlockedAt: string;
}

interface NewAchievementHistoryEntry {
    achievementId?: string;
    userId?: string;
    title: string;
    description: string;
    emoji?: string;
    imageUrl?: string;
    rarity?: AchievementRarity;
    xp?: number;
    coins?: number;
    unlockedAt?: string;
}

type AchievementHistoryListener = (entries: AchievementHistoryEntry[]) => void;

const STORAGE_KEY = 'jellyfin.reward.achievementHistory.v1';
const MAX_HISTORY_ENTRIES = 400;

const listeners = new Set<AchievementHistoryListener>();
let historyEntries: AchievementHistoryEntry[] = [];

function getStorage(): Storage | null {
    if (typeof window === 'undefined') {
        return null;
    }

    try {
        return window.localStorage;
    } catch (error) {
        console.warn('[achievementHistoryStore] localStorage unavailable', error);
        return null;
    }
}

function normalizeRarity(value: unknown): AchievementRarity {
    if (value === 'uncommon' || value === 'rare' || value === 'epic' || value === 'legendary') {
        return value;
    }

    return 'common';
}

function normalizeString(value: unknown): string {
    if (typeof value !== 'string') {
        return '';
    }

    return value.trim();
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

function normalizeEntry(value: unknown): AchievementHistoryEntry | null {
    if (!value || typeof value !== 'object') {
        return null;
    }

    const source = value as Record<string, unknown>;
    const title = normalizeString(source.title);
    const description = normalizeString(source.description);
    if (!title || !description) {
        return null;
    }

    const unlockedAtRaw = normalizeString(source.unlockedAt);
    const unlockedDate = unlockedAtRaw ? new Date(unlockedAtRaw) : null;
    const unlockedAt = unlockedDate && !Number.isNaN(unlockedDate.getTime()) ? unlockedDate.toISOString() : new Date().toISOString();

    const xpRaw = Number(source.xp);
    const xp = Number.isFinite(xpRaw) ? xpRaw : 0;
    const coinsRaw = Number(source.coins);
    const coins = Number.isFinite(coinsRaw) ? coinsRaw : 0;

    return {
        id: normalizeString(source.id) || createEntryId(),
        achievementId: normalizeString(source.achievementId) || undefined,
        userId: normalizeString(source.userId) || undefined,
        title,
        description,
        emoji: normalizeString(source.emoji) || undefined,
        imageUrl: normalizeString(source.imageUrl) || undefined,
        rarity: normalizeRarity(source.rarity),
        xp,
        coins,
        unlockedAt
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
        console.warn('[achievementHistoryStore] failed to persist history', error);
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
            .filter((entry): entry is AchievementHistoryEntry => Boolean(entry))
            .sort((a, b) => new Date(b.unlockedAt).getTime() - new Date(a.unlockedAt).getTime())
            .slice(0, MAX_HISTORY_ENTRIES);
    } catch (error) {
        console.warn('[achievementHistoryStore] failed to load history', error);
        historyEntries = [];
    }
}

loadInitialHistory();

export function addAchievementHistoryEntry(entry: NewAchievementHistoryEntry): AchievementHistoryEntry {
    const normalized = normalizeEntry({
        ...entry,
        id: createEntryId(),
        rarity: normalizeRarity(entry.rarity),
        xp: Number.isFinite(entry.xp) ? Number(entry.xp) : 0,
        coins: Number.isFinite(entry.coins) ? Number(entry.coins) : 0,
        unlockedAt: entry.unlockedAt || new Date().toISOString()
    });

    if (!normalized) {
        throw new Error('Invalid achievement history entry');
    }

    historyEntries = [normalized, ...historyEntries]
        .slice(0, MAX_HISTORY_ENTRIES);

    persistHistory();
    emitHistoryUpdate();

    return normalized;
}

export function getAchievementHistory(userId?: string): AchievementHistoryEntry[] {
    if (!userId) {
        return historyEntries.slice();
    }

    return historyEntries.filter(entry => entry.userId === userId);
}

export function subscribeAchievementHistory(listener: AchievementHistoryListener): () => void {
    listeners.add(listener);
    listener(historyEntries.slice());

    return () => {
        listeners.delete(listener);
    };
}

export function clearAchievementHistory(userId?: string): void {
    if (!userId) {
        historyEntries = [];
    } else {
        historyEntries = historyEntries.filter(entry => entry.userId !== userId);
    }

    persistHistory();
    emitHistoryUpdate();
}
