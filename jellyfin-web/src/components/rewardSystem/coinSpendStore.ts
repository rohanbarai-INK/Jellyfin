interface CoinSpendMap {
    [userId: string]: number;
}

const STORAGE_KEY = 'jellyfin.reward.coinSpendByUser.v1';

let spendMap: CoinSpendMap | null = null;

function getStorage(): Storage | null {
    if (typeof window === 'undefined') {
        return null;
    }

    try {
        return window.localStorage;
    } catch {
        return null;
    }
}

function loadSpendMap(): CoinSpendMap {
    if (spendMap) {
        return spendMap;
    }

    const storage = getStorage();
    if (!storage) {
        spendMap = {};
        return spendMap;
    }

    try {
        const raw = storage.getItem(STORAGE_KEY);
        if (!raw) {
            spendMap = {};
            return spendMap;
        }

        const parsed = JSON.parse(raw) as unknown;
        if (!parsed || typeof parsed !== 'object') {
            spendMap = {};
            return spendMap;
        }

        const loaded: CoinSpendMap = {};
        Object.entries(parsed as Record<string, unknown>).forEach(([userId, value]) => {
            if (!userId) {
                return;
            }

            const parsedValue = Number(value);
            if (!Number.isFinite(parsedValue) || parsedValue <= 0) {
                return;
            }

            loaded[userId] = Math.floor(parsedValue);
        });

        spendMap = loaded;
        return spendMap;
    } catch {
        spendMap = {};
        return spendMap;
    }
}

function persistSpendMap(): void {
    const storage = getStorage();
    if (!storage || !spendMap) {
        return;
    }

    try {
        storage.setItem(STORAGE_KEY, JSON.stringify(spendMap));
    } catch {
        // Ignore persistence failures and keep runtime state.
    }
}

export function getSpentCoins(userId?: string): number {
    if (!userId) {
        return 0;
    }

    const map = loadSpendMap();
    const amount = Number(map[userId]);
    if (!Number.isFinite(amount) || amount <= 0) {
        return 0;
    }

    return Math.floor(amount);
}

export function addSpentCoins(userId: string, amount: number): number {
    const normalizedUserId = userId?.trim();
    const normalizedAmount = Number(amount);
    if (!normalizedUserId || !Number.isFinite(normalizedAmount) || normalizedAmount <= 0) {
        return getSpentCoins(normalizedUserId);
    }

    const map = loadSpendMap();
    const existing = getSpentCoins(normalizedUserId);
    const updated = existing + Math.floor(normalizedAmount);
    map[normalizedUserId] = updated;
    persistSpendMap();
    return updated;
}

export function clearSpentCoins(userId?: string): void {
    if (!userId) {
        spendMap = {};
    } else {
        const normalizedUserId = userId.trim();
        const map = loadSpendMap();
        delete map[normalizedUserId];
    }

    persistSpendMap();
}
