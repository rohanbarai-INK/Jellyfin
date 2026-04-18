import React, { useEffect, useMemo, useState, type FC } from 'react';
import { useSearchParams } from 'react-router-dom';

import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import {
    addActivityRewardHistoryEntry,
    getActivityRewardTotals,
    subscribeActivityRewardHistory
} from 'components/rewardSystem/activityRewardHistoryStore';
import {
    addAchievementHistoryEntry,
    getAchievementHistory as getLocalAchievementHistory,
    subscribeAchievementHistory,
    type AchievementHistoryEntry
} from 'components/rewardSystem/achievementHistoryStore';
import { RewardSystem } from 'components/rewardSystem/RewardSystem';
import { useApi } from 'hooks/useApi';
import { useUsers } from 'hooks/useUsers';
import {
    getAchievementDefinitions,
    getAchievementHistory as getServerAchievementHistory,
    type UserAchievementRow
} from 'utils/achievementsApi';
import {
    getLevelForTotalXp,
    getLifetimeMilestoneCoins,
    getMilestoneReward,
    getNextMilestoneLevel,
    getTotalXpForLevel,
    getXpRequiredForLevel
} from 'utils/levelRewards';

import RankBadge from 'components/rewardSystem/RankBadge';

import './achievements.scss';

const DEFAULT_TOTAL_ACHIEVEMENTS = 100;
const DEMO_USER_NAME = 'baraibrothers';
const SERVER_HISTORY_SYNC_VISIBLE_MS = 20000;
const SERVER_HISTORY_SYNC_HIDDEN_MS = 60000;
const COIN_PREVIEW_AMOUNT = 5;
const ACHIEVEMENT_PREVIEW_PAYLOAD = {
    achievement: {
        id: 'preview-achievement-overlay',
        title: 'Overlay Preview',
        description: 'Achievement first, then the coin overlay appears next.',
        emoji: '\uD83E\uDEA9',
        rarity: 'rare' as const,
        duration: 2100,
        historyDisabled: true
    },
    xpEarned: 25,
    coinsEarned: COIN_PREVIEW_AMOUNT
};
const DEMO_ACHIEVEMENTS = [
    {
        achievementId: 'first-stream',
        title: 'First Stream',
        description: 'Played your first title.',
        emoji: '\u25B6\uFE0F',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'feature-film',
        title: 'Feature Film',
        description: 'Completed your first movie.',
        emoji: '\uD83C\uDFAC',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'long-haul',
        title: 'Long Haul',
        description: 'Watched a movie longer than 3 hours.',
        emoji: '\u23F3',
        rarity: 'rare',
        xp: 200,
        coins: 40
    },
    {
        achievementId: 'action-fan',
        title: 'Action Fan',
        description: 'Watched your first action title.',
        emoji: '\uD83D\uDCA5',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'double-feature',
        title: 'Double Feature',
        description: 'Watched two titles in one day.',
        emoji: '\uD83C\uDF9E\uFE0F',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'weekend-viewer',
        title: 'Weekend Viewer',
        description: 'Watched content during the weekend.',
        emoji: '\uD83D\uDCC6',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'movie-buff',
        title: 'Movie Buff',
        description: 'Completed 5 movies.',
        emoji: '\uD83C\uDFAC',
        rarity: 'common',
        xp: 25,
        coins: 5
    },
    {
        achievementId: 'cinema-lover',
        title: 'Cinema Lover',
        description: 'Completed 15 movies.',
        emoji: '\uD83C\uDFA5',
        rarity: 'uncommon',
        xp: 75,
        coins: 15
    }
] as const;

function formatUnlockedAt(value: string) {
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
        return value;
    }

    return date.toLocaleString([], {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

function toHistoryEntry(row: UserAchievementRow, userId: string): AchievementHistoryEntry {
    return {
        id: `${row.id}:${row.unlockedAt}`,
        achievementId: row.id,
        userId: userId || undefined,
        title: row.title,
        description: row.description,
        emoji: row.imageEmoji,
        rarity: row.rarity,
        xp: row.xp,
        coins: row.coins,
        unlockedAt: row.unlockedAt,
        isSeasonal: row.isSeasonal,
        seasonType: row.seasonType,
        seasonYear: row.seasonYear ?? undefined
    };
}

function mergeHistoryEntries(primary: AchievementHistoryEntry[], secondary: AchievementHistoryEntry[]) {
    const merged = new Map<string, AchievementHistoryEntry>();

    [ ...primary, ...secondary ].forEach(entry => {
        const key = `${entry.userId || ''}:${entry.achievementId || entry.title}:${entry.unlockedAt}`;
        if (!merged.has(key)) {
            merged.set(key, entry);
        }
    });

    return [ ...merged.values() ]
        .sort((a, b) => new Date(b.unlockedAt).getTime() - new Date(a.unlockedAt).getTime());
}

const AchievementHistoryPage: FC = () => {
    const { user: currentUser, __legacyApiClient__: apiClient } = useApi();
    const { data: users } = useUsers();
    const [ searchParams ] = useSearchParams();

    const requestedUserId = useMemo(() => {
        return searchParams.get('userId') || currentUser?.Id || '';
    }, [ currentUser?.Id, searchParams ]);

    const targetUserName = useMemo(() => {
        if (!requestedUserId) {
            return '';
        }

        if (requestedUserId === currentUser?.Id) {
            return currentUser?.Name || '';
        }

        return users?.find(user => user.Id === requestedUserId)?.Name || '';
    }, [ currentUser?.Id, currentUser?.Name, requestedUserId, users ]);

    const [ history, setHistory ] = useState<AchievementHistoryEntry[]>(() => getLocalAchievementHistory(requestedUserId));
    const [ totalAchievements, setTotalAchievements ] = useState<number>(DEFAULT_TOTAL_ACHIEVEMENTS);
    const [ activityTotals, setActivityTotals ] = useState(() => getActivityRewardTotals(requestedUserId));

    useEffect(() => {
        setHistory(getLocalAchievementHistory(requestedUserId));

        return subscribeAchievementHistory((entries) => {
            if (!requestedUserId) {
                setHistory(entries);
                return;
            }

            setHistory(entries.filter(entry => entry.userId === requestedUserId));
        });
    }, [ requestedUserId ]);

    useEffect(() => {
        setActivityTotals(getActivityRewardTotals(requestedUserId));

        return subscribeActivityRewardHistory(() => {
            setActivityTotals(getActivityRewardTotals(requestedUserId));
        });
    }, [ requestedUserId ]);

    useEffect(() => {
        if (!apiClient) {
            return () => undefined;
        }

        let isCancelled = false;

        const loadDefinitions = async () => {
            try {
                const rows = await getAchievementDefinitions(true, apiClient);
                if (!isCancelled) {
                    setTotalAchievements(rows.length || DEFAULT_TOTAL_ACHIEVEMENTS);
                }
            } catch (error) {
                console.warn('[AchievementHistoryPage] failed to load definitions', error);
            }
        };

        void loadDefinitions();

        return () => {
            isCancelled = true;
        };
    }, [ apiClient ]);

    useEffect(() => {
        if (!apiClient) {
            return () => undefined;
        }

        let isCancelled = false;
        let isSyncInFlight = false;
        let syncTimer: number | null = null;

        const clearSyncTimer = () => {
            if (syncTimer != null) {
                window.clearTimeout(syncTimer);
                syncTimer = null;
            }
        };

        const loadServerHistory = async () => {
            if (isCancelled || isSyncInFlight) {
                return;
            }

            isSyncInFlight = true;
            try {
                const rows = await getServerAchievementHistory(requestedUserId || undefined, 400, apiClient);
                if (isCancelled) {
                    return;
                }

                const serverHistory = rows.map(row => toHistoryEntry(row, requestedUserId));
                setHistory(current => mergeHistoryEntries(serverHistory, current));
            } catch (error) {
                console.warn('[AchievementHistoryPage] failed to load server history', error);
            } finally {
                isSyncInFlight = false;
            }
        };

        const scheduleNextSync = (delayMs: number) => {
            clearSyncTimer();
            syncTimer = window.setTimeout(() => {
                void loadServerHistory();
                if (isCancelled) {
                    return;
                }

                scheduleNextSync(document.visibilityState === 'visible'
                    ? SERVER_HISTORY_SYNC_VISIBLE_MS
                    : SERVER_HISTORY_SYNC_HIDDEN_MS);
            }, Math.max(0, delayMs));
        };

        const handleVisibilityChange = () => {
            if (document.visibilityState !== 'visible') {
                return;
            }

            void loadServerHistory();
            scheduleNextSync(SERVER_HISTORY_SYNC_VISIBLE_MS);
        };

        void loadServerHistory();
        scheduleNextSync(SERVER_HISTORY_SYNC_VISIBLE_MS);
        document.addEventListener('visibilitychange', handleVisibilityChange);

        return () => {
            isCancelled = true;
            clearSyncTimer();
            document.removeEventListener('visibilitychange', handleVisibilityChange);
        };
    }, [ apiClient, requestedUserId ]);

    useEffect(() => {
        const normalizedUserName = targetUserName.trim().toLowerCase();
        if (!requestedUserId || normalizedUserName !== DEMO_USER_NAME) {
            return;
        }

        const existing = getLocalAchievementHistory(requestedUserId);
        const existingIds = new Set(existing.map(entry => entry.achievementId).filter(Boolean));
        const missing = DEMO_ACHIEVEMENTS.filter(entry => !existingIds.has(entry.achievementId));
        if (missing.length === 0) {
            return;
        }

        const start = Date.now();
        missing.forEach((entry, index) => {
            addAchievementHistoryEntry({
                userId: requestedUserId,
                achievementId: entry.achievementId,
                title: entry.title,
                description: entry.description,
                emoji: entry.emoji,
                rarity: entry.rarity,
                xp: entry.xp,
                coins: entry.coins,
                unlockedAt: new Date(start - ((index + 1) * 11 * 60 * 1000)).toISOString()
            });
        });
    }, [ requestedUserId, targetUserName ]);

    const achievementTotals = useMemo(() => {
        const unlockedIds = new Set<string>();
        let xp = 0;
        let coins = 0;

        history.forEach(entry => {
            xp += Number(entry.xp) || 0;
            coins += Number(entry.coins) || 0;

            const key = entry.achievementId || `${entry.title}:${entry.unlockedAt}`;
            unlockedIds.add(key);
        });

        return {
            unlockedCount: unlockedIds.size,
            xp,
            coins
        };
    }, [ history ]);

    const totalXp = achievementTotals.xp + activityTotals.xp;
    const level = getLevelForTotalXp(totalXp);
    const milestoneCoins = getLifetimeMilestoneCoins(level);
    const totalCoins = achievementTotals.coins + activityTotals.coins + milestoneCoins;
    const xpAtCurrentLevelStart = getTotalXpForLevel(level);
    const xpForNextLevel = getXpRequiredForLevel(level + 1);
    const xpInCurrentLevel = Math.max(0, totalXp - xpAtCurrentLevelStart);
    const progress = xpForNextLevel > 0
        ? Math.max(0, Math.min(100, (xpInCurrentLevel / xpForNextLevel) * 100))
        : 100;
    const nextLevel = level + 1;
    const nextMilestoneLevel = getNextMilestoneLevel(level);
    const nextMilestoneReward = getMilestoneReward(nextMilestoneLevel);
    const xpToMilestone = Math.max(0, getTotalXpForLevel(nextMilestoneLevel) - totalXp);
    const normalizedUserName = targetUserName.trim().toLowerCase();
    const shouldShowOverlayPreviewControls = normalizedUserName === DEMO_USER_NAME;
    const {
        currentSeasonYear,
        permanentHistory,
        currentSeasonHistory,
        pastSeasonHistory
    } = useMemo(() => {
        const seasonYear = new Date().getFullYear();
        const permanent: AchievementHistoryEntry[] = [];
        const seasonalCurrent: AchievementHistoryEntry[] = [];
        const pastSeasonMap = new Map<number, AchievementHistoryEntry[]>();

        history.forEach((entry) => {
            if (!entry.isSeasonal) {
                permanent.push(entry);
                return;
            }

            if (!Number.isFinite(entry.seasonYear)) {
                seasonalCurrent.push(entry);
                return;
            }

            const entrySeasonYear = Number(entry.seasonYear);
            if (entrySeasonYear >= seasonYear) {
                seasonalCurrent.push(entry);
                return;
            }

            const existing = pastSeasonMap.get(entrySeasonYear);
            if (existing) {
                existing.push(entry);
                return;
            }

            pastSeasonMap.set(entrySeasonYear, [ entry ]);
        });

        const pastSeasons = [ ...pastSeasonMap.entries() ]
            .sort((left, right) => right[0] - left[0])
            .map(([ season, entries ]) => ({
                season,
                entries
            }));

        return {
            currentSeasonYear: seasonYear,
            permanentHistory: permanent,
            currentSeasonHistory: seasonalCurrent,
            pastSeasonHistory: pastSeasons
        };
    }, [ history ]);

    const renderHistoryRows = (entries: AchievementHistoryEntry[]) => entries.map(entry => {
        const badge = entry.imageUrl ? (
            <img
                src={entry.imageUrl}
                alt=''
                className='achievementHistoryBadgeImage'
            />
        ) : (
            <span className='achievementHistoryBadgeEmoji'>
                {entry.emoji || '\uD83C\uDFC6'}
            </span>
        );

        return (
            <div
                key={entry.id}
                className='achievementHistoryItem'
            >
                <div className='achievementHistoryIcon'>
                    {badge}
                </div>
                <div className='achievementHistoryBody'>
                    <div className='achievementHistoryTitle'>
                        {entry.title}
                    </div>
                    <div className='achievementHistoryDescription secondary'>
                        {entry.description}
                    </div>
                </div>
                <div className='achievementHistoryMeta'>
                    <div className='achievementHistoryRewards'>
                        <span className='achievementHistoryRewardPill achievementHistoryRewardPill-xp'>
                            +{entry.xp} XP
                        </span>
                        <span className='achievementHistoryRewardPill achievementHistoryRewardPill-coins'>
                            +{entry.coins} Coins
                        </span>
                    </div>
                    <span className='achievementHistoryTimestamp'>
                        {formatUnlockedAt(entry.unlockedAt)}
                    </span>
                </div>
            </div>
        );
    });

    const triggerCoinOverlayPreview = () => {
        RewardSystem.enqueue({
            coinsEarned: COIN_PREVIEW_AMOUNT
        });
    };

    const triggerAchievementThenCoinPreview = () => {
        RewardSystem.enqueue(ACHIEVEMENT_PREVIEW_PAYLOAD);
    };

    const prepareMilestoneDummyState = () => {
        if (!requestedUserId) {
            return;
        }

        const levelBeforeMilestone = Math.max(0, nextMilestoneLevel - 1);
        const xpRequired = getTotalXpForLevel(levelBeforeMilestone) - totalXp;
        if (xpRequired <= 0) {
            return;
        }

        addActivityRewardHistoryEntry({
            userId: requestedUserId,
            xp: xpRequired,
            coins: 0
        });
    };

    const triggerMilestoneLevelUpDummy = () => {
        const xpRequired = Math.max(1, getTotalXpForLevel(nextMilestoneLevel) - totalXp);
        RewardSystem.enqueue({
            xpEarned: xpRequired,
            coinsEarned: 0
        });
    };

    if (!currentUser) {
        return <Loading />;
    }

    return (
        <Page
            id='achievementsPage'
            className='libraryPage userPreferencesPage noSecondaryNavPage mainAnimatedPage'
            title='Achievements'
            shouldAutoFocus
        >
            <div className='padded-left padded-right padded-bottom-page padded-top achievementsPageContent'>
                <div
                    className='readOnlyContent'
                    style={{
                        margin: '0 auto',
                        maxWidth: '70rem'
                    }}
                >
                    <div className='verticalSection verticalSection-extrabottompadding'>
                        <div className='achievementsHeaderRow'>
                            <h2
                                className='sectionTitle headerUsername'
                                style={{
                                    paddingLeft: '0.25em'
                                }}
                            >
                                {targetUserName ? `${targetUserName} - Achievements` : 'Achievements'}
                            </h2>
                            <RankBadge level={level} />
                        </div>
                        <section className='achievementsHudCard'>
                            <div className='achievementsHudGlow' />

                            <div className='achievementsHudTopRow'>
                                <div>
                                    <div className='achievementsHudMetaLabel'>Current Level</div>
                                    <div className='achievementsHudLevelValue'>{level}</div>
                                </div>

                                <div className='achievementsHudTotalsCol'>
                                    <div className='achievementsHudMetaLabel'>Total XP</div>
                                    <div className='achievementsHudTotalValue'>{totalXp.toLocaleString()}</div>
                                    <div className='achievementsHudCoinsValue'>
                                        {totalCoins.toLocaleString()} Coins | {milestoneCoins.toLocaleString()} milestone
                                    </div>
                                </div>
                            </div>

                            <div className='achievementsHudProgressMeta'>
                                <span>{xpInCurrentLevel.toLocaleString()} XP</span>
                                <span>{xpForNextLevel.toLocaleString()} XP</span>
                            </div>
                            <div className='achievementsHudProgressTrack'>
                                <div
                                    className='achievementsHudProgressFill'
                                    style={{ width: `${progress}%` }}
                                />
                                <div className='achievementsHudProgressShine' />
                            </div>
                            <div className='achievementsHudProgressNext'>Next: Lv {nextLevel}</div>

                            <div className='achievementsHudCategoryGrid'>
                                <article className='achievementsHudCategoryCard'>
                                    <div className='achievementsHudCategoryTitle'>ACTIVITY-BASED REWARDS</div>
                                    <div className='achievementsHudCategoryValue'>+{activityTotals.xp.toLocaleString()} XP</div>
                                    <div className='achievementsHudCategoryMeta'>{activityTotals.count.toLocaleString()} reward events</div>
                                </article>

                                <article className='achievementsHudCategoryCard'>
                                    <div className='achievementsHudCategoryTitle'>ACHIEVEMENTS ({totalAchievements.toLocaleString()} TOTAL)</div>
                                    <div className='achievementsHudCategoryValue'>+{achievementTotals.xp.toLocaleString()} XP</div>
                                    <div className='achievementsHudCategoryMeta'>{achievementTotals.unlockedCount.toLocaleString()} unlocked</div>
                                </article>
                            </div>

                            <div className='achievementsHudMilestone'>
                                <span className='material-icons achievementsHudMilestoneIcon' aria-hidden='true'>
                                    lock
                                </span>
                                <div className='achievementsHudMilestoneBody'>
                                    <div className='achievementsHudMilestoneTitle'>
                                        Next Big Reward: Level {nextMilestoneLevel}
                                    </div>
                                    <div className='achievementsHudMilestoneText'>
                                        Reward: {nextMilestoneReward.toLocaleString()} COINS
                                    </div>
                                    <div className='achievementsHudMilestoneHint'>
                                        {xpToMilestone.toLocaleString()} XP to unlock
                                    </div>
                                </div>
                            </div>
                        </section>

                        {shouldShowOverlayPreviewControls && (
                            <section className='achievementsPreviewControls' aria-label='Overlay Preview Controls'>
                                <div className='achievementsPreviewTitle'>Overlay Preview</div>
                                <div className='achievementsPreviewActions'>
                                    <button
                                        type='button'
                                        className='achievementsPreviewButton'
                                        onClick={triggerCoinOverlayPreview}
                                    >
                                        Trigger Coin Overlay (+{COIN_PREVIEW_AMOUNT})
                                    </button>
                                    <button
                                        type='button'
                                        className='achievementsPreviewButton achievementsPreviewButton-secondary'
                                        onClick={triggerAchievementThenCoinPreview}
                                    >
                                        Trigger Unlock Then Coins
                                    </button>
                                    <button
                                        type='button'
                                        className='achievementsPreviewButton'
                                        onClick={prepareMilestoneDummyState}
                                    >
                                        Prepare Milestone Test
                                    </button>
                                    <button
                                        type='button'
                                        className='achievementsPreviewButton achievementsPreviewButton-secondary'
                                        onClick={triggerMilestoneLevelUpDummy}
                                    >
                                        Trigger Milestone Level-Up
                                    </button>
                                </div>
                                <div className='achievementsPreviewHint'>
                                    Milestone test: prepare state, trigger level-up, then click CLAIM to see coin overlay.
                                </div>
                            </section>
                        )}

                        {history.length === 0 && (
                            <div className='listItem'>
                                <span className='material-icons listItemIcon listItemIcon-transparent emoji_events' aria-hidden='true' />
                                <div className='listItemBody'>
                                    <div className='listItemBodyText'>
                                        No achievements unlocked yet.
                                    </div>
                                </div>
                            </div>
                        )}

                        {history.length > 0 && (
                            <div className='achievementsRecentHeader'>
                                Recent Achievements
                            </div>
                        )}

                        {permanentHistory.length > 0 && (
                            <>
                                <div className='achievementsHistorySectionHeader'>
                                    Permanent
                                </div>
                                {renderHistoryRows(permanentHistory)}
                            </>
                        )}

                        {currentSeasonHistory.length > 0 && (
                            <>
                                <div className='achievementsHistorySectionHeader'>
                                    Seasonal ({currentSeasonYear})
                                </div>
                                {renderHistoryRows(currentSeasonHistory)}
                            </>
                        )}

                        {pastSeasonHistory.length > 0 && (
                            <div className='achievementsPastSeasonSection'>
                                <div className='achievementsHistorySectionHeader'>
                                    Past Seasons
                                </div>
                                {pastSeasonHistory.map(group => (
                                    <details key={group.season} className='achievementsPastSeasonGroup'>
                                        <summary className='achievementsPastSeasonSummary'>
                                            {group.season} Season ({group.entries.length})
                                        </summary>
                                        <div className='achievementsPastSeasonRows'>
                                            {renderHistoryRows(group.entries)}
                                        </div>
                                    </details>
                                ))}
                            </div>
                        )}
                    </div>
                </div>
            </div>

        </Page>
    );
};

export default AchievementHistoryPage;

