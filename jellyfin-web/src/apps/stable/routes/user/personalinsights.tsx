import React, { FunctionComponent, useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import { useApi } from 'hooks/useApi';
import { useUsers } from 'hooks/useUsers';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import '../../../../styles/personalinsights.scss';

type PeriodType = 'month' | 'year' | 'all';

interface PersonalInsightsSummary {
    totalWatchHours: number;
    watchTimeChangePercent: number;
    moviesWatched: number;
    moviesDelta: number;
    episodesWatched: number;
    episodesDelta: number;
    engagementScore: 'High' | 'Medium' | 'Low';
    engagementPercentile: number;
}

interface HourlyDistributionPoint {
    hour: number;
    minutes: number;
}

interface PersonalInsightsPeakViewing {
    hourlyDistribution: HourlyDistributionPoint[];
    peakHour: number;
    label: string;
}

interface ContinueWatchingItem {
    itemId: string;
    title: string;
    seriesName: string;
    seasonNumber: number | null;
    episodeNumber: number | null;
    remainingMinutes: number;
    imageUrl: string;
}

interface RecentBinge {
    seriesName: string;
    episodeCount: number;
}

interface BingePayload {
    longestStreak: number;
    recentBinges: RecentBinge[];
}

interface GenrePayload {
    name: string;
    minutes: number;
    percentage: number;
}

interface DonutSegment {
    index: number;
    name: string;
    minutes: number;
    percentage: number;
    color: string;
    dashArray: string;
    dashOffset: number;
}

interface PersonalInsightsResponse {
    summary: PersonalInsightsSummary;
    peakViewing: PersonalInsightsPeakViewing;
    continueWatching: ContinueWatchingItem[];
    binge: BingePayload;
    genres: GenrePayload[];
    insightText: string;
}

const donutColors = ['#8b5cf6', '#6366f1', '#3b82f6'];

const toNumber = (value: unknown, fallback = 0): number => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
};

const toStringValue = (value: unknown, fallback = ''): string => {
    if (typeof value === 'string') {
        return value;
    }

    return fallback;
};

const normalizeInsightsResponse = (payload: unknown): PersonalInsightsResponse => {
    const root = (payload || {}) as Record<string, unknown>;
    const summarySource = ((root.summary || root.Summary) || {}) as Record<string, unknown>;
    const peakSource = ((root.peakViewing || root.PeakViewing) || {}) as Record<string, unknown>;
    const bingeSource = ((root.binge || root.Binge) || {}) as Record<string, unknown>;
    const continueSource = ((root.continueWatching || root.ContinueWatching) || []) as Array<Record<string, unknown>>;
    const genreSource = ((root.genres || root.Genres) || []) as Array<Record<string, unknown>>;
    const hourlySource = ((peakSource.hourlyDistribution || peakSource.HourlyDistribution) || []) as Array<Record<string, unknown>>;
    const recentBingesSource = ((bingeSource.recentBinges || bingeSource.RecentBinges) || []) as Array<Record<string, unknown>>;

    return {
        summary: {
            totalWatchHours: toNumber(summarySource.totalWatchHours ?? summarySource.TotalWatchHours),
            watchTimeChangePercent: toNumber(summarySource.watchTimeChangePercent ?? summarySource.WatchTimeChangePercent),
            moviesWatched: Math.round(toNumber(summarySource.moviesWatched ?? summarySource.MoviesWatched)),
            moviesDelta: Math.round(toNumber(summarySource.moviesDelta ?? summarySource.MoviesDelta)),
            episodesWatched: Math.round(toNumber(summarySource.episodesWatched ?? summarySource.EpisodesWatched)),
            episodesDelta: Math.round(toNumber(summarySource.episodesDelta ?? summarySource.EpisodesDelta)),
            engagementScore: toStringValue(summarySource.engagementScore ?? summarySource.EngagementScore, 'Low') as 'High' | 'Medium' | 'Low',
            engagementPercentile: Math.round(toNumber(summarySource.engagementPercentile ?? summarySource.EngagementPercentile))
        },
        peakViewing: {
            hourlyDistribution: hourlySource.map(point => ({
                hour: Math.max(0, Math.min(23, Math.round(toNumber(point.hour ?? point.Hour)))),
                minutes: toNumber(point.minutes ?? point.Minutes)
            })),
            peakHour: Math.max(0, Math.min(23, Math.round(toNumber(peakSource.peakHour ?? peakSource.PeakHour)))),
            label: toStringValue(peakSource.label ?? peakSource.Label, 'Night Owl')
        },
        continueWatching: continueSource.map(item => ({
            itemId: toStringValue(item.itemId ?? item.ItemId),
            title: toStringValue(item.title ?? item.Title),
            seriesName: toStringValue(item.seriesName ?? item.SeriesName),
            seasonNumber: item.seasonNumber ?? item.SeasonNumber ? Math.round(toNumber(item.seasonNumber ?? item.SeasonNumber)) : null,
            episodeNumber: item.episodeNumber ?? item.EpisodeNumber ? Math.round(toNumber(item.episodeNumber ?? item.EpisodeNumber)) : null,
            remainingMinutes: toNumber(item.remainingMinutes ?? item.RemainingMinutes),
            imageUrl: toStringValue(item.imageUrl ?? item.ImageUrl)
        })),
        binge: {
            longestStreak: Math.round(toNumber(bingeSource.longestStreak ?? bingeSource.LongestStreak)),
            recentBinges: recentBingesSource.map(item => ({
                seriesName: toStringValue(item.seriesName ?? item.SeriesName),
                episodeCount: Math.round(toNumber(item.episodeCount ?? item.EpisodeCount))
            }))
        },
        genres: genreSource.map(item => ({
            name: toStringValue(item.name ?? item.Name),
            minutes: toNumber(item.minutes ?? item.Minutes),
            percentage: toNumber(item.percentage ?? item.Percentage)
        })),
        insightText: toStringValue(root.insightText ?? root.InsightText)
    };
};

const formatHour = (value: number | string): string => {
    const hour = Number(value);
    if (Number.isNaN(hour)) {
        return '';
    }

    if (hour === 0) {
        return '12 AM';
    }

    if (hour < 12) {
        return `${hour} AM`;
    }

    if (hour === 12) {
        return '12 PM';
    }

    return `${hour - 12} PM`;
};

const formatSignedValue = (value: number, suffix = ''): string => {
    const rounded = Math.round(value * 10) / 10;
    const sign = rounded > 0 ? '+' : '';
    return `${sign}${rounded}${suffix}`;
};

const formatWatchHours = (hours: number): string => {
    if (Math.abs(hours - Math.round(hours)) < 0.1) {
        return `${Math.round(hours)}h`;
    }

    return `${hours.toFixed(1)}h`;
};

const formatMinutes = (minutes: number): string => {
    const safeMinutes = Math.max(0, minutes);
    if (safeMinutes <= 0) {
        return '0';
    }

    if (safeMinutes < 1) {
        return '<1';
    }

    return safeMinutes < 10
        ? safeMinutes.toFixed(1).replace(/\.0$/, '')
        : Math.round(safeMinutes).toString();
};

const formatMinutesText = (minutes: number): string => {
    const safeMinutes = Math.max(0, minutes);
    if (safeMinutes <= 0) {
        return '0 minutes';
    }

    if (safeMinutes < 1) {
        return 'less than 1 minute';
    }

    const rounded = safeMinutes < 10
        ? Number(safeMinutes.toFixed(1))
        : Math.round(safeMinutes);
    return `${rounded} minute${rounded === 1 ? '' : 's'}`;
};

const formatHourWithMinute = (hourValue: number, minuteValue: number): string => {
    const hour = Number(hourValue);
    if (Number.isNaN(hour)) {
        return '';
    }

    const suffix = hour < 12 ? 'AM' : 'PM';
    const hour12 = hour % 12 === 0 ? 12 : hour % 12;
    const minute = Math.max(0, Math.min(59, Math.trunc(minuteValue)));
    return `${hour12}:${String(minute).padStart(2, '0')} ${suffix}`;
};

const formatHourRange = (hourValue: number): string => {
    const hour = Number(hourValue);
    if (Number.isNaN(hour)) {
        return '';
    }

    return `${formatHourWithMinute(hour, 0)} - ${formatHourWithMinute(hour, 59)}`;
};

const PersonalInsightsPage: FunctionComponent = () => {
    const [ searchParams, setSearchParams ] = useSearchParams();
    const { user: currentUser } = useApi();
    const { data: users } = useUsers();
    const requestedPeriod = searchParams.get('period');
    const requestedUserId = searchParams.get('userId') || currentUser?.Id || '';
    const initialPeriod: PeriodType =
        requestedPeriod === 'month' || requestedPeriod === 'year' || requestedPeriod === 'all'
            ? requestedPeriod
            : 'month';

    const [ period, setPeriod ] = useState<PeriodType>(initialPeriod);
    const [ insights, setInsights ] = useState<PersonalInsightsResponse | null>(null);
    const [ isLoading, setIsLoading ] = useState(true);
    const [ errorMessage, setErrorMessage ] = useState('');
    const [ hoveredHour, setHoveredHour ] = useState<number | null>(null);
    const [ hoveredGenreIndex, setHoveredGenreIndex ] = useState<number | null>(null);

    const targetUserName = useMemo(() => {
        if (requestedUserId && requestedUserId === currentUser?.Id) {
            return currentUser?.Name || '';
        }

        return users?.find(user => user.Id === requestedUserId)?.Name || currentUser?.Name || '';
    }, [currentUser?.Id, currentUser?.Name, requestedUserId, users]);

    const loadInsights = useCallback(async () => {
        const apiClient = ServerConnections.currentApiClient();
        if (!apiClient) {
            setErrorMessage('Unable to reach server API.');
            setIsLoading(false);
            return;
        }

        setIsLoading(true);
        setErrorMessage('');

        try {
            const queryString = new URLSearchParams({ period });
            if (requestedUserId) {
                queryString.set('userId', requestedUserId);
            }

            const response = await apiClient.ajax({
                type: 'GET',
                url: apiClient.getUrl(`api/activity/personal-insights?${queryString.toString()}`),
                dataType: 'json'
            });

            setInsights(normalizeInsightsResponse(response));
        } catch (error) {
            console.error('[personalinsights] failed to load data', error);
            setErrorMessage('Failed to load personal insights.');
        } finally {
            setIsLoading(false);
        }
    }, [period, requestedUserId]);

    useEffect(() => {
        void loadInsights();
    }, [loadInsights]);

    const onPeriodChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
        const nextPeriod = event.target.value as PeriodType;
        setPeriod(nextPeriod);

        const nextSearchParams = new URLSearchParams(searchParams);
        nextSearchParams.set('period', nextPeriod);
        setSearchParams(nextSearchParams);
    };

    const hourlyDistribution = useMemo(() => {
        if (!insights?.peakViewing.hourlyDistribution?.length) {
            return Array.from({ length: 24 }, (_, hour) => ({ hour, minutes: 0 }));
        }

        const hourlyMap = new Map<number, number>();
        for (const point of insights.peakViewing.hourlyDistribution) {
            hourlyMap.set(point.hour, point.minutes);
        }

        return Array.from({ length: 24 }, (_, hour) => ({
            hour,
            minutes: hourlyMap.get(hour) ?? 0
        }));
    }, [insights]);

    const maxHourlyMinutes = useMemo(() => {
        const maxValue = hourlyDistribution.reduce((maxMinutes, point) => Math.max(maxMinutes, point.minutes), 0);
        return Math.max(1, maxValue);
    }, [hourlyDistribution]);

    const topGenres = useMemo(() => insights?.genres.slice(0, 3) ?? [], [insights?.genres]);

    const donutSegments = useMemo<DonutSegment[]>(() => {
        const circumference = 2 * Math.PI * 42;
        const input = topGenres.map((genre, index) => ({
            index,
            name: genre.name,
            minutes: genre.minutes,
            percentage: Math.max(0, genre.percentage),
            color: donutColors[index % donutColors.length]
        }));
        const totalPercentage = input.reduce((sum, genre) => sum + genre.percentage, 0);
        const scaleFactor = totalPercentage > 100 ? 100 / totalPercentage : 1;
        let runningPercentage = 0;

        return input.map((genre) => {
            const normalizedPercentage = genre.percentage * scaleFactor;
            const segmentLength = circumference * (normalizedPercentage / 100);
            const dashArray = `${segmentLength} ${Math.max(0, circumference - segmentLength)}`;
            const dashOffset = -(circumference * (runningPercentage / 100));
            runningPercentage += normalizedPercentage;
            return {
                ...genre,
                percentage: normalizedPercentage,
                dashArray,
                dashOffset
            };
        });
    }, [topGenres]);

    const hoveredHourPoint = useMemo(() => {
        if (hoveredHour === null) {
            return null;
        }

        return hourlyDistribution.find(point => point.hour === hoveredHour) ?? null;
    }, [hourlyDistribution, hoveredHour]);

    const hoveredGenre = useMemo(() => {
        if (topGenres.length === 0) {
            return null;
        }

        if (hoveredGenreIndex === null) {
            return topGenres[0];
        }

        return topGenres[hoveredGenreIndex] ?? topGenres[0];
    }, [hoveredGenreIndex, topGenres]);

    const periodLabel = period === 'month' ? 'this month' : period === 'year' ? 'this year' : 'all time';
    const subtitleLabel = period === 'month' ? 'this month' : period === 'year' ? 'this year' : 'all time';

    return (
        <Page
            id='personalInsightsPage'
            className='mainAnimatedPage libraryPage userPreferencesPage noSecondaryNavPage'
            title='Personal Insights'
        >
            <div className='padded-left padded-right padded-bottom-page padded-top'>
                <div className='readOnlyContent personalInsightsContent'>
                    <div className='personalInsightsHeader'>
                        <div>
                            <h2 className='personalInsightsTitle'>Welcome back, {targetUserName || 'Viewer'}</h2>
                            <p className='personalInsightsSubtitle'>Here&apos;s your personal viewing insight for {subtitleLabel}.</p>
                        </div>
                        <label className='personalInsightsPeriodLabel'>
                            Period
                            <select
                                className='personalInsightsPeriodSelect'
                                value={period}
                                onChange={onPeriodChange}
                                aria-label='Personal insights period'
                            >
                                <option value='month'>Month</option>
                                <option value='year'>Year</option>
                                <option value='all'>AllTime</option>
                            </select>
                        </label>
                    </div>

                    {errorMessage && (
                        <div className='personalInsightsError'>
                            {errorMessage}
                        </div>
                    )}

                    {isLoading || !insights ? (
                        <Loading />
                    ) : (
                        <>
                            <div className='personalInsightsCardGrid'>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsCardTitle'>Total Watch Time</div>
                                    <div className='personalInsightsCardValue'>{formatWatchHours(insights.summary.totalWatchHours)}</div>
                                    <div className='personalInsightsCardTrend'>{formatSignedValue(insights.summary.watchTimeChangePercent, '%')} vs previous period</div>
                                </div>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsCardTitle'>Movies Watched</div>
                                    <div className='personalInsightsCardValue'>{insights.summary.moviesWatched}</div>
                                    <div className='personalInsightsCardTrend'>{formatSignedValue(insights.summary.moviesDelta)} vs previous period</div>
                                </div>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsCardTitle'>Episodes Watched</div>
                                    <div className='personalInsightsCardValue'>{insights.summary.episodesWatched}</div>
                                    <div className='personalInsightsCardTrend'>{formatSignedValue(insights.summary.episodesDelta)} vs previous period</div>
                                </div>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsCardTitle'>Engagement Score</div>
                                    <div className='personalInsightsCardValue'>{insights.summary.engagementScore}</div>
                                    <div className='personalInsightsCardTrend'>Top {insights.summary.engagementPercentile}%</div>
                                </div>
                            </div>

                            <div className='personalInsightsCard'>
                                <div className='personalInsightsSectionTitle'>Peak Viewing Hours</div>
                                <p className='personalInsightsSectionText'>
                                    You are most active around {formatHour(insights.peakViewing.peakHour)}. Looks like you&apos;re a {insights.peakViewing.label}!
                                </p>
                                <div className='personalInsightsHistogram'>
                                    {hourlyDistribution.map((point) => (
                                        <div
                                            key={`hour-${point.hour}`}
                                            className={`personalInsightsBarColumn${point.hour === insights.peakViewing.peakHour ? ' isPeak' : ''}`}
                                            title={`${formatHourRange(point.hour)}: ${formatMinutesText(point.minutes)}`}
                                            tabIndex={0}
                                            role='button'
                                            aria-label={`${formatHourRange(point.hour)} - ${formatMinutesText(point.minutes)} in ${subtitleLabel}`}
                                            onMouseEnter={() => setHoveredHour(point.hour)}
                                            onMouseLeave={() => setHoveredHour(null)}
                                            onFocus={() => setHoveredHour(point.hour)}
                                            onBlur={() => setHoveredHour(null)}
                                            onClick={() => setHoveredHour(point.hour)}
                                        >
                                            <div
                                                className='personalInsightsBarFill'
                                                style={{ height: `${Math.max(0, (point.minutes / maxHourlyMinutes) * 100)}%` }}
                                            />
                                        </div>
                                    ))}
                                </div>
                                <div className='personalInsightsHistogramAxis'>
                                    {hourlyDistribution.map((point) => (
                                        <div key={`hour-label-${point.hour}`} className='personalInsightsAxisLabel'>
                                            {point.hour % 4 === 0 ? formatHour(point.hour) : ''}
                                        </div>
                                    ))}
                                </div>
                                {hoveredHourPoint && (
                                    <div className='personalInsightsHoverInfo'>
                                        {`${formatHour(hoveredHourPoint.hour)} (${formatHourRange(hoveredHourPoint.hour)}): ${formatMinutesText(hoveredHourPoint.minutes)} in ${subtitleLabel}.`}
                                    </div>
                                )}
                            </div>

                            <div className='personalInsightsSplitGrid'>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsSectionTitle'>Continue Watching</div>
                                    {insights.continueWatching.length === 0 && (
                                        <div className='personalInsightsEmpty'>No in-progress items.</div>
                                    )}
                                    {insights.continueWatching.map((item) => (
                                        <div key={item.itemId} className='personalInsightsContinueRow'>
                                            <div className='personalInsightsContinueMeta'>
                                                <div className='personalInsightsContinueTitle'>{item.seriesName || item.title}</div>
                                                <div className='personalInsightsContinueSubtitle'>
                                                    {item.seasonNumber && item.episodeNumber
                                                        ? `S${item.seasonNumber}:E${item.episodeNumber} - ${item.title}`
                                                        : item.title}
                                                </div>
                                            </div>
                                            <div className='personalInsightsContinueRemaining'>{Math.max(0, Math.round(item.remainingMinutes))}m</div>
                                        </div>
                                    ))}
                                </div>

                                <div className='personalInsightsCard personalInsightsBingeCard'>
                                    <div className='personalInsightsSectionTitle'>Binge Master</div>
                                    <div className='personalInsightsBingeValue'>
                                        Longest Streak: {insights.binge.longestStreak} episodes
                                    </div>
                                    <div className='personalInsightsRecentTitle'>Recent binge sessions</div>
                                    {insights.binge.recentBinges.length === 0 && (
                                        <div className='personalInsightsEmpty'>No recent binge sessions.</div>
                                    )}
                                    {insights.binge.recentBinges.map((binge, index) => (
                                        <div key={`binge-${index}`} className='personalInsightsBingeRow'>
                                            <span>{binge.seriesName || 'Unknown series'}</span>
                                            <span>{binge.episodeCount} eps</span>
                                        </div>
                                    ))}
                                </div>
                            </div>

                            <div className='personalInsightsSplitGrid'>
                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsSectionTitle'>Genre Preference</div>
                                    <div className='personalInsightsGenreLayout'>
                                        <div className='personalInsightsDonut'>
                                            <svg className='personalInsightsDonutSvg' viewBox='0 0 100 100' role='img' aria-label='Top genre distribution'>
                                                <circle className='personalInsightsDonutTrack' cx='50' cy='50' r='42' />
                                                {donutSegments.map((segment) => (
                                                    <circle
                                                        key={segment.name}
                                                        cx='50'
                                                        cy='50'
                                                        r='42'
                                                        className={`personalInsightsDonutSegment${hoveredGenreIndex === segment.index ? ' isActive' : ''}`}
                                                        style={{
                                                            stroke: segment.color,
                                                            strokeDasharray: segment.dashArray,
                                                            strokeDashoffset: segment.dashOffset
                                                        }}
                                                        tabIndex={segment.percentage > 0 ? 0 : -1}
                                                        role='button'
                                                        aria-label={`${segment.name} - ${formatMinutesText(segment.minutes)} (${Math.round(segment.percentage)}%) in ${subtitleLabel}`}
                                                        onMouseEnter={() => setHoveredGenreIndex(segment.index)}
                                                        onMouseLeave={() => setHoveredGenreIndex(null)}
                                                        onFocus={() => setHoveredGenreIndex(segment.index)}
                                                        onBlur={() => setHoveredGenreIndex(null)}
                                                        onClick={() => setHoveredGenreIndex(segment.index)}
                                                    >
                                                        <title>{`${segment.name}: ${formatMinutesText(segment.minutes)} (${Math.round(segment.percentage)}%)`}</title>
                                                    </circle>
                                                ))}
                                            </svg>
                                            <div className='personalInsightsDonutCenter'>
                                                <span className='personalInsightsDonutCenterMain'>
                                                    {hoveredGenre ? `${Math.round(hoveredGenre.percentage)}%` : '0%'}
                                                </span>
                                                <span className='personalInsightsDonutCenterSub'>
                                                    {hoveredGenre?.name || 'No data'}
                                                </span>
                                            </div>
                                        </div>
                                        <div className='personalInsightsGenreList'>
                                            {topGenres.map((genre, index) => (
                                                <div
                                                    key={genre.name}
                                                    className={`personalInsightsGenreRow${hoveredGenreIndex === index ? ' isActive' : ''}`}
                                                    tabIndex={0}
                                                    role='button'
                                                    onMouseEnter={() => setHoveredGenreIndex(index)}
                                                    onMouseLeave={() => setHoveredGenreIndex(null)}
                                                    onFocus={() => setHoveredGenreIndex(index)}
                                                    onBlur={() => setHoveredGenreIndex(null)}
                                                    onClick={() => setHoveredGenreIndex(index)}
                                                    aria-label={`${genre.name} - ${formatMinutesText(genre.minutes)} (${Math.round(genre.percentage)}%)`}
                                                >
                                                    <span className='personalInsightsGenreName'>
                                                        <span className='personalInsightsGenreDot' style={{ background: donutColors[index % donutColors.length] }} />
                                                        {genre.name}
                                                    </span>
                                                    <span>{formatMinutes(genre.minutes)}m ({Math.round(genre.percentage)}%)</span>
                                                </div>
                                            ))}
                                        </div>
                                    </div>
                                    {hoveredGenre && (
                                        <div className='personalInsightsHoverInfo'>
                                            {`${hoveredGenre.name}: ${formatMinutesText(hoveredGenre.minutes)} (${Math.round(hoveredGenre.percentage)}%) in ${subtitleLabel}.`}
                                        </div>
                                    )}
                                </div>

                                <div className='personalInsightsCard'>
                                    <div className='personalInsightsSectionTitle'>Smart Insight</div>
                                    <p className='personalInsightsInsightText'>
                                        {insights.insightText || `You've spent 22% watching Sci-Fi ${periodLabel}.`}
                                    </p>
                                </div>
                            </div>
                        </>
                    )}
                </div>
            </div>
        </Page>
    );
};

export default PersonalInsightsPage;
