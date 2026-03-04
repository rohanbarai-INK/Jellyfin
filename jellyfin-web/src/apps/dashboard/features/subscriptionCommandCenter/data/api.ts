import { ServerConnections } from 'lib/jellyfin-apiclient';

export interface OverviewStats {
    activeUsers: number;
    graceUsers: number;
    expiredUsers: number;
    expiringSoon: number;
    totalRevenue: number;
    keysGenerated: number;
    keysRedeemed: number;
    unusedKeys: number;
}

export interface ExpiryRadar {
    next24h: number;
    next3days: number;
    next7days: number;
    next30days: number;
}

export interface ExpiringUser {
    userId: string;
    username: string;
    expiryDate: string;
    daysRemaining: number;
    plan: string;
}

export interface KeyStats {
    totalGenerated: number;
    redeemed: number;
    unused: number;
    expired: number;
}

export interface CohortData {
    month: string;
    usersJoined: number;
    renewalRate: number;
}

export interface SystemHealth {
    renewalRate: number;
    activeUsers: number;
    monthlyGrowth: number;
}

export interface DashboardSnapshot {
    overview: OverviewStats;
    radar: ExpiryRadar;
    keyStats: KeyStats;
    cohorts: CohortData[];
    health: SystemHealth;
}

export interface GeneratedKey {
    key: string;
    duration: string;
    prefix: string;
    batchName: string;
    resellerTag: string;
    createdAt: string;
}

export interface BulkGeneratePayload {
    durationMonths: number;
    quantity: number;
    prefix: string;
    batchName: string;
    resellerTag: string;
}

type JsonRecord = Record<string, unknown>;

function toRecord(value: unknown): JsonRecord {
    if (!value || typeof value !== 'object') {
        return {};
    }

    return value as JsonRecord;
}

function pickValue(record: JsonRecord, pascalCase: string, camelCase: string): unknown {
    if (record[pascalCase] !== undefined) {
        return record[pascalCase];
    }

    return record[camelCase];
}

function toNumber(value: unknown, fallback = 0): number {
    const numberValue = typeof value === 'number' ? value : Number(value);
    return Number.isFinite(numberValue) ? numberValue : fallback;
}

function toString(value: unknown, fallback = ''): string {
    return typeof value === 'string' ? value : fallback;
}

function durationMonthsToLabel(months: number): string {
    return months === 1 ? '1 Month' : `${months} Months`;
}

function parseOverview(payload: unknown): OverviewStats {
    const record = toRecord(payload);

    return {
        activeUsers: toNumber(pickValue(record, 'ActiveUsers', 'activeUsers')),
        graceUsers: toNumber(pickValue(record, 'GraceUsers', 'graceUsers')),
        expiredUsers: toNumber(pickValue(record, 'ExpiredUsers', 'expiredUsers')),
        expiringSoon: toNumber(pickValue(record, 'ExpiringSoon', 'expiringSoon')),
        totalRevenue: toNumber(pickValue(record, 'TotalRevenue', 'totalRevenue')),
        keysGenerated: toNumber(pickValue(record, 'KeysGenerated', 'keysGenerated')),
        keysRedeemed: toNumber(pickValue(record, 'KeysRedeemed', 'keysRedeemed')),
        unusedKeys: toNumber(pickValue(record, 'UnusedKeys', 'unusedKeys'))
    };
}

function parseRadar(payload: unknown): ExpiryRadar {
    const record = toRecord(payload);

    return {
        next24h: toNumber(pickValue(record, 'Next24h', 'next24h')),
        next3days: toNumber(pickValue(record, 'Next3days', 'next3days')),
        next7days: toNumber(pickValue(record, 'Next7days', 'next7days')),
        next30days: toNumber(pickValue(record, 'Next30days', 'next30days'))
    };
}

function parseKeyStats(payload: unknown): KeyStats {
    const record = toRecord(payload);

    return {
        totalGenerated: toNumber(pickValue(record, 'TotalGenerated', 'totalGenerated')),
        redeemed: toNumber(pickValue(record, 'Redeemed', 'redeemed')),
        unused: toNumber(pickValue(record, 'Unused', 'unused')),
        expired: toNumber(pickValue(record, 'Expired', 'expired'))
    };
}

function parseCohorts(payload: unknown): CohortData[] {
    if (!Array.isArray(payload)) {
        return [];
    }

    return payload.map((item) => {
        const record = toRecord(item);

        return {
            month: toString(pickValue(record, 'Month', 'month')),
            usersJoined: toNumber(pickValue(record, 'UsersJoined', 'usersJoined')),
            renewalRate: toNumber(pickValue(record, 'RenewalRate', 'renewalRate'))
        };
    });
}

function parseSystemHealth(payload: unknown): SystemHealth {
    const record = toRecord(payload);

    return {
        renewalRate: toNumber(pickValue(record, 'RenewalRate', 'renewalRate')),
        activeUsers: toNumber(pickValue(record, 'ActiveUsers', 'activeUsers')),
        monthlyGrowth: toNumber(pickValue(record, 'MonthlyGrowth', 'monthlyGrowth'))
    };
}

function parseExpiringUsers(payload: unknown): ExpiringUser[] {
    if (!Array.isArray(payload)) {
        return [];
    }

    return payload.map((item) => {
        const record = toRecord(item);

        return {
            userId: toString(pickValue(record, 'UserId', 'userId')),
            username: toString(pickValue(record, 'Username', 'username')),
            expiryDate: toString(pickValue(record, 'ExpiryDate', 'expiryDate')),
            daysRemaining: toNumber(pickValue(record, 'DaysRemaining', 'daysRemaining')),
            plan: toString(pickValue(record, 'Plan', 'plan'), 'N/A')
        };
    });
}

function getApiClient() {
    const apiClient = ServerConnections.currentApiClient();
    if (!apiClient) {
        throw new Error('Unable to reach server API.');
    }

    return apiClient;
}

function getErrorStatus(errorValue: unknown): number | undefined {
    if (errorValue instanceof Response) {
        return errorValue.status;
    }

    const error = errorValue as {
        status?: number;
        response?: {
            status?: number;
        };
    };

    return error.response?.status ?? error.status;
}

function parseGeneratedKeys(
    payload: unknown,
    request: BulkGeneratePayload): GeneratedKey[] {
    const response = toRecord(payload);
    const items = pickValue(response, 'Items', 'items');
    if (!Array.isArray(items)) {
        return [];
    }

    return items
        .map((item) => {
            const record = toRecord(item);
            const key = toString(pickValue(record, 'Key', 'key'));
            if (!key) {
                return null;
            }

            const createdAt = toString(
                pickValue(record, 'CreatedAt', 'createdAt'),
                new Date().toISOString());

            return {
                key,
                duration: durationMonthsToLabel(request.durationMonths),
                prefix: request.prefix,
                batchName: request.batchName,
                resellerTag: request.resellerTag,
                createdAt
            };
        })
        .filter((item): item is GeneratedKey => item !== null);
}

async function generateKeysFallback(
    request: BulkGeneratePayload): Promise<GeneratedKey[]> {
    const apiClient = getApiClient();
    const keys: GeneratedKey[] = [];

    for (let index = 0; index < request.quantity; index += 1) {
        const response = await apiClient.ajax({
            type: 'POST',
            url: apiClient.getUrl('Keys/Generate'),
            data: JSON.stringify({
                DurationMonths: request.durationMonths
            }),
            dataType: 'json',
            contentType: 'application/json'
        });

        const row = toRecord(response);
        const key = toString(pickValue(row, 'Key', 'key'));
        if (!key) {
            continue;
        }

        keys.push({
            key,
            duration: durationMonthsToLabel(request.durationMonths),
            prefix: request.prefix,
            batchName: request.batchName,
            resellerTag: request.resellerTag,
            createdAt: toString(pickValue(row, 'CreatedAt', 'createdAt'), new Date().toISOString())
        });
    }

    return keys;
}

export async function fetchDashboardSnapshot(): Promise<DashboardSnapshot> {
    const apiClient = getApiClient();
    const response = await apiClient.ajax({
        type: 'GET',
        url: apiClient.getUrl('Keys/AdminDashboard'),
        dataType: 'json'
    });

    const payload = toRecord(response);
    const overviewPayload = pickValue(payload, 'Overview', 'overview');
    const radarPayload = pickValue(payload, 'ExpiryRadar', 'expiryRadar');
    const keyStatsPayload = pickValue(payload, 'KeyStats', 'keyStats');
    const cohortsPayload = pickValue(payload, 'Cohorts', 'cohorts');
    const healthPayload = pickValue(payload, 'SystemHealth', 'systemHealth');

    return {
        overview: parseOverview(overviewPayload),
        radar: parseRadar(radarPayload),
        keyStats: parseKeyStats(keyStatsPayload),
        cohorts: parseCohorts(cohortsPayload),
        health: parseSystemHealth(healthPayload)
    };
}

export async function fetchExpiringUsers(days = 7): Promise<ExpiringUser[]> {
    const apiClient = getApiClient();
    const safeDays = Math.max(1, Math.min(365, days));
    const response = await apiClient.ajax({
        type: 'GET',
        url: apiClient.getUrl(`Keys/AdminExpiringUsers?days=${safeDays}`),
        dataType: 'json'
    });

    return parseExpiringUsers(response);
}

export async function bulkGenerateKeys(payload: BulkGeneratePayload): Promise<GeneratedKey[]> {
    const apiClient = getApiClient();

    try {
        const response = await apiClient.ajax({
            type: 'POST',
            url: apiClient.getUrl('Keys/GenerateBulk'),
            data: JSON.stringify({
                DurationMonths: payload.durationMonths,
                Quantity: payload.quantity
            }),
            dataType: 'json',
            contentType: 'application/json'
        });

        const keys = parseGeneratedKeys(response, payload);
        if (keys.length > 0) {
            return keys;
        }
    } catch (errorValue: unknown) {
        const statusCode = getErrorStatus(errorValue);
        if (statusCode !== 404) {
            throw errorValue;
        }
    }

    return generateKeysFallback(payload);
}
