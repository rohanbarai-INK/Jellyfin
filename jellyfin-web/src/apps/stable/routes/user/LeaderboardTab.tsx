import React, { useCallback, useEffect, useRef, useState, type FC } from 'react';

import { useApi } from 'hooks/useApi';
import {
    getLeaderboardPersonal,
    getLeaderboardTop,
    type LeaderboardCompetition,
    type LeaderboardEntry,
    type LeaderboardMetricType,
    type LeaderboardPersonalStats,
    type LeaderboardTopResult
} from 'utils/leaderboardApi';

import './leaderboard.scss';

interface LeaderboardTabProps {
    seasonYear?: number;
}

interface MetricTab {
    key: LeaderboardMetricType;
    label: string;
    icon: string;
    primaryLabel: string;
    secondaryLabel: string;
}

interface TabCache {
    personal: LeaderboardPersonalStats | null;
    top: LeaderboardTopResult | null;
    loadedEntries: LeaderboardEntry[];
    hasExpandedOnce: boolean;
}

const PAGE_SIZE = 10;

const METRIC_TABS: MetricTab[] = [
    { key: 'xp', label: 'XP', icon: '\uD83C\uDFC6', primaryLabel: 'XP', secondaryLabel: 'Level' },
    { key: 'watchtime', label: 'Watch Time', icon: '\u23F1', primaryLabel: 'Watch Time', secondaryLabel: 'Avg / Week' },
    { key: 'movies', label: 'Movies', icon: '\uD83C\uDFA5', primaryLabel: 'Movies', secondaryLabel: 'Watch Time' },
    { key: 'series', label: 'Series', icon: '\uD83D\uDCFA', primaryLabel: 'Episodes', secondaryLabel: 'Watch Time' },
    { key: 'genres', label: 'Genres', icon: '\uD83C\uDF0D', primaryLabel: 'Genres', secondaryLabel: 'New / Month' },
    { key: 'streak', label: 'Streak', icon: '\uD83D\uDD25', primaryLabel: 'Current Streak', secondaryLabel: 'Best Streak' },
    { key: 'achievements', label: 'Achievements', icon: '\uD83C\uDFC5', primaryLabel: 'Achievements', secondaryLabel: 'Achievement XP' }
];

const getMetricTab = (metric: LeaderboardMetricType) => METRIC_TABS.find(tab => tab.key === metric) || METRIC_TABS[0];

const getInitials = (userName: string) => {
    const clean = userName.trim();
    if (!clean) {
        return 'JF';
    }

    const parts = clean.split(/\s+/).slice(0, 2);
    return parts.map(part => part.charAt(0).toUpperCase()).join('');
};

const getAvatarUrl = (avatarBasePath: string | undefined, entry: LeaderboardEntry) => (
    avatarBasePath && entry.userId && entry.primaryImageTag ?
        `${avatarBasePath}/Users/${entry.userId}/Images/Primary?tag=${entry.primaryImageTag}` :
        undefined
);

const getPercentileLabel = (stats: LeaderboardPersonalStats) => {
    if (stats.totalUsers <= 1) {
        return 'Only contender this season';
    }

    const percentile = Math.max(1, Math.round(100 - stats.percentile));
    return `Top ${percentile}% this season`;
};

const getHeroNarrative = (stats: LeaderboardPersonalStats) => {
    if (stats.nextTarget && stats.behindUser) {
        return `Catch ${stats.nextTarget.entry.userName} with ${stats.nextTarget.gapLabel} more. ${stats.behindUser.entry.userName} is ${stats.behindUser.gapLabel} behind you.`;
    }

    if (stats.nextTarget) {
        return `You need ${stats.nextTarget.gapLabel} to pass ${stats.nextTarget.entry.userName}.`;
    }

    if (stats.behindUser) {
        return `You're leading your local race. ${stats.behindUser.entry.userName} is ${stats.behindUser.gapLabel} behind you.`;
    }

    return 'You are setting the pace on this leaderboard right now.';
};

const getBadgeText = (badgeKind: string) => {
    switch (badgeKind) {
        case 'gold':
            return '#1';
        case 'silver':
            return '#2';
        case 'bronze':
            return '#3';
        case 'top10':
            return 'Top 10';
        default:
            return '';
    }
};

const SummaryHero: FC<{ stats: LeaderboardPersonalStats; metric: MetricTab; seasonYear: number }> = ({ stats, metric, seasonYear }) => {
    const currentUserEntry = stats.currentUserEntry;

    return (
        <section className='leaderboardHero'>
            <div className='leaderboardHeroHeader'>
                <div>
                    <div className='leaderboardHeroEyebrow'>{metric.icon} Season {seasonYear} {metric.label} leaderboard</div>
                    <h3 className='leaderboardHeroTitle'>Know who you need to beat next.</h3>
                </div>
                <div className='leaderboardHeroTier'>
                    <span className='leaderboardHeroTierEmoji'>{stats.rankEmoji}</span>
                    <span>{stats.rankTitle}</span>
                </div>
            </div>

            <div className='leaderboardHeroStats'>
                <article className='leaderboardHeroStatCard'>
                    <div className='leaderboardHeroStatLabel'>Your Rank</div>
                    <div className='leaderboardHeroStatValue'>#{stats.rank}</div>
                    <div className='leaderboardHeroStatMeta'>{getPercentileLabel(stats)}</div>
                </article>
                <article className='leaderboardHeroStatCard'>
                    <div className='leaderboardHeroStatLabel'>{metric.primaryLabel}</div>
                    <div className='leaderboardHeroStatValue'>{currentUserEntry?.metricLabel || stats.metricLabel}</div>
                    <div className='leaderboardHeroStatMeta'>{metric.secondaryLabel}: {currentUserEntry?.secondaryMetricLabel || stats.rankTitle}</div>
                </article>
                <article className='leaderboardHeroStatCard'>
                    <div className='leaderboardHeroStatLabel'>Competition Pool</div>
                    <div className='leaderboardHeroStatValue'>{stats.totalUsers.toLocaleString()}</div>
                    <div className='leaderboardHeroStatMeta'>Users ranked in this season</div>
                </article>
            </div>

            <div className='leaderboardHeroNarrative'>
                {getHeroNarrative(stats)}
            </div>
        </section>
    );
};

const LeaderboardRow: FC<{
    avatarBasePath?: string;
    contextLabel?: string;
    contextMessage?: string;
    entry: LeaderboardEntry;
    metric: MetricTab;
    testId?: string;
}> = ({ avatarBasePath, contextLabel, contextMessage, entry, metric, testId }) => {
    const avatarUrl = getAvatarUrl(avatarBasePath, entry);
    const badgeKind = entry.badgeKind || 'none';
    const rowClassName = [
        'leaderboardTableRow',
        entry.isCurrentUser ? 'leaderboardTableRow-current' : '',
        badgeKind !== 'none' ? `leaderboardTableRow-${badgeKind}` : ''
    ].filter(Boolean).join(' ');
    const badgeText = getBadgeText(badgeKind);

    return (
        <div
            className={rowClassName}
            data-testid={testId || 'leaderboard-row'}
        >
            <div className='leaderboardRowIdentity'>
                {contextLabel && (
                    <div className='leaderboardRowContextLabel'>{contextLabel}</div>
                )}
                <div className='leaderboardRowIdentityMain'>
                    <div className={`leaderboardRowRank leaderboardRowRank-${badgeKind}`}>#{entry.rank}</div>
                    <div className={`leaderboardRowAvatar leaderboardRowAvatar-${badgeKind}`}>
                        {avatarUrl ? (
                            <img src={avatarUrl} alt='' className='leaderboardRowAvatarImage' />
                        ) : (
                            <span className='leaderboardRowAvatarFallback'>{getInitials(entry.userName)}</span>
                        )}
                        {badgeKind !== 'none' && (
                            <span className={`leaderboardRowAvatarBadge leaderboardRowAvatarBadge-${badgeKind}`}>
                                {badgeText}
                            </span>
                        )}
                    </div>
                    <div className='leaderboardRowNameBlock'>
                        <div className='leaderboardRowNameLine'>
                            <span className='leaderboardRowName'>{entry.userName || 'Unknown User'}</span>
                            {entry.isCurrentUser && <span className='leaderboardRowYouPill'>You</span>}
                        </div>
                        <div className='leaderboardRowSubtitle'>{entry.rankEmoji} {entry.rankTitle}</div>
                        {contextMessage && <div className='leaderboardRowContextMessage'>{contextMessage}</div>}
                    </div>
                </div>
            </div>

            <div className='leaderboardRowMetrics'>
                <div className='leaderboardRowMetric leaderboardRowMetric-primary'>
                    <span className='leaderboardRowMetricLabel'>{metric.primaryLabel}</span>
                    <span className='leaderboardRowMetricValue'>{entry.metricLabel}</span>
                </div>
                <div className='leaderboardRowMetric leaderboardRowMetric-secondary'>
                    <span className='leaderboardRowMetricLabel'>{metric.secondaryLabel}</span>
                    <span className='leaderboardRowMetricValue'>{entry.secondaryMetricLabel}</span>
                </div>
            </div>
        </div>
    );
};

const getCompetitionMessage = (competition: LeaderboardCompetition | null, direction: 'next' | 'behind') => {
    if (!competition) {
        return '';
    }

    if (competition.gapValue <= 0) {
        return 'Tied on the current metric';
    }

    return direction === 'next' ?
        `Need ${competition.gapLabel} to pass` :
        `${competition.gapLabel} behind you`;
};

interface InlineCompetitionCard {
    key: string;
    label: string;
    title: string;
    message: string;
    modifier?: 'current' | 'leader' | 'empty';
    testId: string;
}

const getInlineCompetitionCards = (
    stats: LeaderboardPersonalStats,
    currentUserEntry: LeaderboardEntry | null
): InlineCompetitionCard[] => {
    const cards: InlineCompetitionCard[] = [];

    if (stats.nextTarget) {
        cards.push({
            key: 'next',
            label: 'Next Target',
            title: `#${stats.nextTarget.entry.rank} • ${stats.nextTarget.entry.userName}`,
            message: getCompetitionMessage(stats.nextTarget, 'next'),
            testId: 'leaderboard-inline-next-target'
        });
    } else if (stats.rank === 1) {
        cards.push({
            key: 'leader',
            label: 'Lead',
            title: 'You are holding #1',
            message: 'No one is ahead of you on this metric.',
            modifier: 'leader',
            testId: 'leaderboard-inline-lead'
        });
    }

    if (currentUserEntry) {
        cards.push({
            key: 'current',
            label: 'Your Rank',
            title: `#${currentUserEntry.rank} • ${currentUserEntry.userName}`,
            message: `${currentUserEntry.metricLabel || stats.metricLabel} • ${getPercentileLabel(stats)}`,
            modifier: 'current',
            testId: 'leaderboard-inline-current-user'
        });
    }

    if (stats.behindUser) {
        cards.push({
            key: 'behind',
            label: 'Behind You',
            title: `#${stats.behindUser.entry.rank} • ${stats.behindUser.entry.userName}`,
            message: getCompetitionMessage(stats.behindUser, 'behind'),
            testId: 'leaderboard-inline-behind-user'
        });
    } else if (stats.rank === 1 && stats.totalUsers <= 1) {
        cards.push({
            key: 'empty',
            label: 'Competition Pool',
            title: 'Only contender so far',
            message: 'Once more users post seasonal stats, they will appear here.',
            modifier: 'empty',
            testId: 'leaderboard-inline-competition-pool'
        });
    }

    return cards;
};

const InlineCompetitionStrip: FC<{
    currentUserEntry: LeaderboardEntry | null;
    stats: LeaderboardPersonalStats;
}> = ({ currentUserEntry, stats }) => {
    const cards = getInlineCompetitionCards(stats, currentUserEntry);

    if (cards.length === 0) {
        return null;
    }

    return (
        <>
            <div className='leaderboardTableSectionDivider' data-testid='leaderboard-inline-competition-section'>
                Your competition
            </div>
            <div className='leaderboardInlineCompetition' data-testid='leaderboard-inline-competition'>
                {cards.map(card => {
                    const cardClassName = card.modifier ?
                        `leaderboardInlineCompetitionCard leaderboardInlineCompetitionCard-${card.modifier}` :
                        'leaderboardInlineCompetitionCard';

                    return (
                        <div
                            key={card.key}
                            className={cardClassName}
                            data-testid={card.testId}
                        >
                            <div className='leaderboardInlineCompetitionLabel'>{card.label}</div>
                            <div className='leaderboardInlineCompetitionTitle'>{card.title}</div>
                            <div className='leaderboardInlineCompetitionMessage'>{card.message}</div>
                        </div>
                    );
                })}
            </div>
        </>
    );
};

const EMPTY_DATA: TabCache = {
    personal: null,
    top: null,
    loadedEntries: [],
    hasExpandedOnce: false
};

const LeaderboardTab: FC<LeaderboardTabProps> = ({ seasonYear }) => {
    const { __legacyApiClient__: apiClient, api } = useApi();
    const [activeMetric, setActiveMetric] = useState<LeaderboardMetricType>('xp');
    const [isLoading, setIsLoading] = useState(true);
    const [isLoadingMore, setIsLoadingMore] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const cacheRef = useRef<Record<string, TabCache>>({});
    const [currentData, setCurrentData] = useState<TabCache>(EMPTY_DATA);

    const loadMetric = useCallback(async (metric: LeaderboardMetricType) => {
        if (!apiClient) {
            return;
        }

        const cacheKey = `${metric}_${seasonYear || 'current'}`;
        const cached = cacheRef.current[cacheKey];
        if (cached) {
            setCurrentData(cached);
            setIsLoading(false);
            return;
        }

        setIsLoading(true);
        setError(null);

        try {
            const [personal, top] = await Promise.all([
                getLeaderboardPersonal(seasonYear, metric, apiClient),
                getLeaderboardTop(seasonYear, PAGE_SIZE, metric, apiClient, 0)
            ]);

            const data: TabCache = {
                personal,
                top,
                loadedEntries: top.entries,
                hasExpandedOnce: false
            };

            cacheRef.current[cacheKey] = data;
            setCurrentData(data);
        } catch (err) {
            console.warn('[LeaderboardTab] failed to load data', err);
            setError('Failed to load leaderboard data.');
            setCurrentData(EMPTY_DATA);
        } finally {
            setIsLoading(false);
        }
    }, [apiClient, seasonYear]);

    useEffect(() => {
        void loadMetric(activeMetric);
    }, [activeMetric, loadMetric]);

    const handleMetricClick = useCallback((event: React.MouseEvent<HTMLButtonElement>) => {
        const metric = event.currentTarget.dataset.metric as LeaderboardMetricType | undefined;
        if (metric) {
            setActiveMetric(metric);
        }
    }, []);

    const handleLoadMore = useCallback(async () => {
        if (!apiClient || !currentData.top || isLoadingMore || !currentData.top.hasMore) {
            return;
        }

        setIsLoadingMore(true);

        try {
            const nextPage = await getLeaderboardTop(
                seasonYear,
                PAGE_SIZE,
                activeMetric,
                apiClient,
                currentData.loadedEntries.length
            );

            const mergedEntries = [
                ...currentData.loadedEntries,
                ...nextPage.entries.filter(entry => !currentData.loadedEntries.some(existing => existing.userId === entry.userId && existing.rank === entry.rank))
            ];

            const cacheKey = `${activeMetric}_${seasonYear || 'current'}`;
            const nextData: TabCache = {
                personal: currentData.personal,
                top: nextPage,
                loadedEntries: mergedEntries,
                hasExpandedOnce: true
            };

            cacheRef.current[cacheKey] = nextData;
            setCurrentData(nextData);
        } catch (err) {
            console.warn('[LeaderboardTab] failed to load more rows', err);
            setError('Failed to load more leaderboard rows.');
        } finally {
            setIsLoadingMore(false);
        }
    }, [activeMetric, apiClient, currentData, isLoadingMore, seasonYear]);

    const handleLoadMoreClick = useCallback(() => {
        void handleLoadMore();
    }, [handleLoadMore]);

    const metric = getMetricTab(activeMetric);
    const loadedEntries = currentData.loadedEntries;
    const topEntries = loadedEntries.slice(0, PAGE_SIZE);
    const extraEntries = loadedEntries.slice(PAGE_SIZE);
    const totalUsers = currentData.top?.totalUsers || currentData.personal?.totalUsers || 0;
    const isTopTenUser = !!currentData.personal && currentData.personal.rank <= PAGE_SIZE;
    const showContinuationRow = !!currentData.top?.hasMore && loadedEntries.length === PAGE_SIZE && !currentData.hasExpandedOnce;
    const showLoadMoreRow = !!currentData.top?.hasMore && currentData.hasExpandedOnce;
    const nextRangeStart = loadedEntries.length + 1;
    const nextRangeEnd = Math.min(loadedEntries.length + PAGE_SIZE, totalUsers);
    const currentUserEntry = currentData.personal?.currentUserEntry || currentData.top?.currentUserPosition || null;
    const avatarBasePath = api?.basePath;

    return (
        <div className='leaderboardContainer'>
            <div className='leaderboardMetricTabs'>
                {METRIC_TABS.map(tab => (
                    <button
                        key={tab.key}
                        type='button'
                        data-metric={tab.key}
                        className={`leaderboardMetricTab${activeMetric === tab.key ? ' leaderboardMetricTab-active' : ''}`}
                        onClick={handleMetricClick}
                    >
                        <span className='leaderboardMetricTabIcon'>{tab.icon}</span>
                        <span className='leaderboardMetricTabLabel'>{tab.label}</span>
                    </button>
                ))}
            </div>

            {isLoading && (
                <div className='leaderboardLoadingState'>Loading leaderboard...</div>
            )}

            {error && !isLoading && (
                <div className='leaderboardEmptyState'>
                    <span className='leaderboardEmptyIcon'>⚠️</span>
                    {error}
                </div>
            )}

            {!isLoading && !error && currentData.personal && currentData.top && (
                <>
                    <SummaryHero
                        stats={currentData.personal}
                        metric={metric}
                        seasonYear={currentData.top.seasonYear || seasonYear || new Date().getFullYear()}
                    />

                    <section className='leaderboardBoard' data-testid='leaderboard-table'>
                        <div className='leaderboardBoardHeader'>
                            <div className='leaderboardBoardTitle'>
                                {metric.icon} Top contenders
                            </div>
                            <div className='leaderboardBoardMeta'>
                                Showing {Math.min(loadedEntries.length, totalUsers)} of {totalUsers.toLocaleString()}
                            </div>
                        </div>

                        <div className='leaderboardTableHeader'>
                            <span className='leaderboardTableHeaderPlayer'>Player</span>
                            <span className='leaderboardTableHeaderMetric'>{metric.primaryLabel}</span>
                            <span className='leaderboardTableHeaderMetric'>{metric.secondaryLabel}</span>
                        </div>

                        {loadedEntries.length === 0 && (
                            <div className='leaderboardEmptyState'>
                                <span className='leaderboardEmptyIcon'>🏅</span>
                                No leaderboard entries yet.
                            </div>
                        )}

                        {topEntries.map(entry => (
                            <LeaderboardRow
                                key={`${entry.userId}-${entry.rank}`}
                                avatarBasePath={avatarBasePath}
                                entry={entry}
                                metric={metric}
                            />
                        ))}

                        {isTopTenUser && currentData.personal && (
                            <InlineCompetitionStrip
                                currentUserEntry={currentUserEntry}
                                stats={currentData.personal}
                            />
                        )}

                        {extraEntries.map(entry => (
                            <LeaderboardRow
                                key={`${entry.userId}-${entry.rank}`}
                                avatarBasePath={avatarBasePath}
                                entry={entry}
                                metric={metric}
                            />
                        ))}

                        {showContinuationRow && (
                            <button
                                type='button'
                                className='leaderboardTableActionRow leaderboardTableActionRow-continuation'
                                onClick={handleLoadMoreClick}
                                data-testid='leaderboard-continuation-row'
                            >
                                <span className='leaderboardTableActionDots'>...</span>
                                <span>Show ranks #{nextRangeStart}-{nextRangeEnd}</span>
                            </button>
                        )}

                        {showLoadMoreRow && (
                            <button
                                type='button'
                                className='leaderboardTableActionRow leaderboardTableActionRow-loadMore'
                                onClick={handleLoadMoreClick}
                                disabled={isLoadingMore}
                                data-testid='leaderboard-load-more-row'
                            >
                                <span>{isLoadingMore ? 'Loading more...' : `Load more ranks #${nextRangeStart}-${nextRangeEnd}`}</span>
                            </button>
                        )}

                        {!isTopTenUser && (
                            <>
                                <div className='leaderboardTableSectionDivider' data-testid='leaderboard-competition-section'>
                                    Your competition
                                </div>

                                {currentData.personal.nextTarget && (
                                    <LeaderboardRow
                                        avatarBasePath={avatarBasePath}
                                        contextLabel='Next Target'
                                        contextMessage={getCompetitionMessage(currentData.personal.nextTarget, 'next')}
                                        entry={currentData.personal.nextTarget.entry}
                                        metric={metric}
                                        testId='leaderboard-next-target-row'
                                    />
                                )}

                                {currentUserEntry && (
                                    <LeaderboardRow
                                        avatarBasePath={avatarBasePath}
                                        contextLabel='Your Rank'
                                        contextMessage={`#${currentUserEntry.rank} of ${currentData.personal.totalUsers.toLocaleString()} • ${getPercentileLabel(currentData.personal)}`}
                                        entry={currentUserEntry}
                                        metric={metric}
                                        testId='leaderboard-current-user-row'
                                    />
                                )}

                                {currentData.personal.behindUser && (
                                    <LeaderboardRow
                                        avatarBasePath={avatarBasePath}
                                        contextLabel='Behind You'
                                        contextMessage={getCompetitionMessage(currentData.personal.behindUser, 'behind')}
                                        entry={currentData.personal.behindUser.entry}
                                        metric={metric}
                                        testId='leaderboard-behind-user-row'
                                    />
                                )}
                            </>
                        )}
                    </section>
                </>
            )}
        </div>
    );
};

export default LeaderboardTab;
