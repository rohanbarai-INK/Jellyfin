using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.DbConfiguration;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Database.Implementations.Interfaces;
using Jellyfin.Database.Implementations.Locking;
using Jellyfin.Server.Implementations.Tracking;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.PersonalInsights;
using MediaBrowser.Model.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.Tracking;

public class WatchSessionTrackingAndAggregationTests
{
    [Fact]
    public async Task NormalWatch_UpdatesStats()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var runtimeTicks = TimeSpan.FromMinutes(100).Ticks;
        var validatedTicks = TimeSpan.FromMinutes(95).Ticks;
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Movie",
            RunTimeTicks = runtimeTicks,
            Genres = ["Sci-Fi", "Drama"]
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-normal", 0));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "play-normal", TimeSpan.FromMinutes(10).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(85));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(user, movie, "play-normal", validatedTicks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.IsValidSession);
        Assert.Equal(validatedTicks, session.ValidatedTicks);

        var monthStats = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03");

        Assert.Equal(validatedTicks, monthStats.TotalValidatedTicks);
        Assert.Equal(1, monthStats.SessionCount);
        Assert.Equal(1, monthStats.CompletedMovies);
        Assert.Equal(0, monthStats.CompletedEpisodes);
        Assert.Equal(3, await dbContext.UserPeriodStats.CountAsync(stats => stats.UserId.Equals(userId)));

        var monthHours = await dbContext.UserPeriodHourlyStats
            .Where(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03")
            .ToListAsync();
        Assert.Equal(3, monthHours.Count);
        Assert.Equal(TimeSpan.FromMinutes(30).Ticks, monthHours.Single(stats => stats.Hour == 15).TotalValidatedTicks);
        Assert.Equal(TimeSpan.FromMinutes(60).Ticks, monthHours.Single(stats => stats.Hour == 16).TotalValidatedTicks);
        Assert.Equal(TimeSpan.FromMinutes(5).Ticks, monthHours.Single(stats => stats.Hour == 17).TotalValidatedTicks);
        Assert.Equal(validatedTicks, monthHours.Sum(stats => stats.TotalValidatedTicks));

        var monthGenres = await dbContext.UserGenrePeriodStats
            .Where(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03")
            .ToListAsync();
        Assert.Equal(2, monthGenres.Count);
        Assert.Equal(validatedTicks * 2, monthGenres.Sum(stats => stats.TotalValidatedTicks));
    }

    [Fact]
    public async Task HourlyAggregation_SplitsTicksAcrossTouchedHours()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "Hourly Movie",
            RunTimeTicks = TimeSpan.FromHours(4).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 15, 10, 20, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        var validatedTicks = TimeSpan.FromMinutes(105).Ticks;
        var session = CreateValidSession(
            userId,
            itemId,
            "hourly-split",
            new DateTime(2026, 3, 15, 10, 20, 0, DateTimeKind.Utc),
            validatedTicks);
        await context.PersistAndAggregateAsync(session);

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var monthHours = await dbContext.UserPeriodHourlyStats
            .Where(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03")
            .OrderBy(stats => stats.Hour)
            .ToListAsync();

        Assert.Equal(3, monthHours.Count);
        Assert.Equal(TimeSpan.FromMinutes(10).Ticks, monthHours.Single(stats => stats.Hour == 15).TotalValidatedTicks);
        Assert.Equal(TimeSpan.FromMinutes(60).Ticks, monthHours.Single(stats => stats.Hour == 16).TotalValidatedTicks);
        Assert.Equal(TimeSpan.FromMinutes(35).Ticks, monthHours.Single(stats => stats.Hour == 17).TotalValidatedTicks);
        Assert.Equal(validatedTicks, monthHours.Sum(stats => stats.TotalValidatedTicks));
    }

    [Fact]
    public async Task SeekAbuse_ReducesValidatedTicks()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Seekable Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 16, 14, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-seek", 0));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "play-seek", TimeSpan.FromSeconds(10).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "play-seek", TimeSpan.FromHours(1).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(user, movie, "play-seek", TimeSpan.FromHours(1).Ticks + TimeSpan.FromSeconds(10).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.AccumulatedTicks > session.ValidatedTicks);
        Assert.True(session.SuspicionScore > 0);
        Assert.True(session.ValidatedTicks <= TimeSpan.FromSeconds(30).Ticks);

        var monthStats = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03");
        Assert.Equal(session.ValidatedTicks, monthStats.TotalValidatedTicks);
    }

    [Fact]
    public async Task LongPlayback_IsCappedAtEightHours()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Long Movie",
            RunTimeTicks = TimeSpan.FromHours(20).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 17, 1, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-long", 0));
        context.TimeProvider.Advance(TimeSpan.FromHours(9));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "play-long", TimeSpan.FromHours(9).Ticks));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(user, movie, "play-long", TimeSpan.FromHours(9).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.Equal(TimeSpan.FromHours(8).Ticks, session.AccumulatedTicks);
        Assert.Equal(TimeSpan.FromHours(8).Ticks, session.ValidatedTicks);

        var allTimeStats = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.AllTime
                && stats.PeriodKey == "ALL");
        Assert.Equal(TimeSpan.FromHours(8).Ticks, allTimeStats.TotalValidatedTicks);
    }

    [Fact]
    public async Task MonthChangeover_CreatesNewPeriodKey()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "Month Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 31, 20, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        var marchSession = CreateValidSession(userId, itemId, "march", new DateTime(2026, 3, 31, 22, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(50).Ticks);
        var aprilSession = CreateValidSession(userId, itemId, "april", new DateTime(2026, 4, 1, 1, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30).Ticks);
        await context.PersistAndAggregateAsync(marchSession);
        await context.PersistAndAggregateAsync(aprilSession);

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var monthStats = await dbContext.UserPeriodStats
            .Where(stats => stats.UserId.Equals(userId) && stats.PeriodType == PeriodType.Month)
            .ToListAsync();

        Assert.Contains(monthStats, stats => stats.PeriodKey == "2026-03");
        Assert.Contains(monthStats, stats => stats.PeriodKey == "2026-04");
    }

    [Fact]
    public async Task YearChangeover_AggregatesIntoSeparateYears()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "Year Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2025, 12, 31, 20, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        var firstSession = CreateValidSession(userId, itemId, "2025-last", new DateTime(2025, 12, 31, 23, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(40).Ticks);
        var secondSession = CreateValidSession(userId, itemId, "2026-first", new DateTime(2026, 1, 1, 0, 30, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(20).Ticks);
        await context.PersistAndAggregateAsync(firstSession);
        await context.PersistAndAggregateAsync(secondSession);

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var yearStats = await dbContext.UserPeriodStats
            .Where(stats => stats.UserId.Equals(userId) && stats.PeriodType == PeriodType.Year)
            .ToListAsync();

        Assert.Contains(yearStats, stats => stats.PeriodKey == "2025");
        Assert.Contains(yearStats, stats => stats.PeriodKey == "2026");
    }

    [Fact]
    public async Task AllTime_AlwaysAccumulates()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "AllTime Movie",
            RunTimeTicks = TimeSpan.FromHours(3).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        var firstTicks = TimeSpan.FromMinutes(30).Ticks;
        var secondTicks = TimeSpan.FromMinutes(20).Ticks;
        var thirdTicks = TimeSpan.FromMinutes(10).Ticks;
        await context.PersistAndAggregateAsync(CreateValidSession(userId, itemId, "first", new DateTime(2024, 2, 1, 10, 0, 0, DateTimeKind.Utc), firstTicks, firstTicks + TimeSpan.FromMinutes(5).Ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(userId, itemId, "second", new DateTime(2025, 7, 10, 8, 0, 0, DateTimeKind.Utc), secondTicks, secondTicks + TimeSpan.FromMinutes(5).Ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(userId, itemId, "third", new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc), thirdTicks, thirdTicks + TimeSpan.FromMinutes(5).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var allTime = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.AllTime
                && stats.PeriodKey == "ALL");

        Assert.Equal(firstTicks + secondTicks + thirdTicks, allTime.TotalValidatedTicks);
        Assert.Equal(3, allTime.SessionCount);
    }

    [Fact]
    public async Task BingeDetection_SupportsFullyQualifiedEpisodeType()
    {
        var userId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var episode1Id = Guid.NewGuid();
        var episode2Id = Guid.NewGuid();
        var episode3Id = Guid.NewGuid();

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = episode1Id,
            Name = "Episode 1",
            SeriesId = seriesId,
            ParentIndexNumber = 1,
            IndexNumber = 1,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = episode2Id,
            Name = "Episode 2",
            SeriesId = seriesId,
            ParentIndexNumber = 1,
            IndexNumber = 2,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = episode3Id,
            Name = "Episode 3",
            SeriesId = seriesId,
            ParentIndexNumber = 1,
            IndexNumber = 3,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });

        await using (var dbContext = await context.DbFactory.CreateDbContextAsync())
        {
            var item1 = await dbContext.BaseItems.SingleAsync(item => item.Id.Equals(episode1Id));
            var item2 = await dbContext.BaseItems.SingleAsync(item => item.Id.Equals(episode2Id));
            var item3 = await dbContext.BaseItems.SingleAsync(item => item.Id.Equals(episode3Id));
            item1.Type = typeof(MediaBrowser.Controller.Entities.TV.Episode).FullName!;
            item2.Type = typeof(MediaBrowser.Controller.Entities.TV.Episode).FullName!;
            item3.Type = typeof(MediaBrowser.Controller.Entities.TV.Episode).FullName!;
            await dbContext.SaveChangesAsync();
        }

        var start = new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc);
        var ticks = TimeSpan.FromMinutes(24).Ticks;
        await context.PersistAndAggregateAsync(CreateValidSession(userId, episode1Id, "ep-1", start, ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(userId, episode2Id, "ep-2", start.AddHours(1), ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(userId, episode3Id, "ep-3", start.AddHours(2), ticks));

        await using (var dbContext = await context.DbFactory.CreateDbContextAsync())
        {
            var binge = await dbContext.UserBingeSessions.SingleAsync(row => row.UserId.Equals(userId));
            Assert.Equal(seriesId, binge.SeriesId);
            Assert.Equal(3, binge.EpisodeCount);

            var monthStats = await dbContext.UserPeriodStats
                .SingleAsync(stats => stats.UserId.Equals(userId)
                    && stats.PeriodType == PeriodType.Month
                    && stats.PeriodKey == "2026-04");
            Assert.Equal(1, monthStats.BingeSessions);
        }
    }

    [Fact]
    public async Task GenreAggregation_UsesSeriesGenresWhenEpisodeGenresMissing()
    {
        var userId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Series
        {
            Id = seriesId,
            Name = "Series With Genre",
            Genres = ["Drama"]
        });
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = episodeId,
            Name = "Episode Without Genres",
            SeriesId = seriesId,
            ParentIndexNumber = 1,
            IndexNumber = 1,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });

        var validatedTicks = TimeSpan.FromMinutes(24).Ticks;
        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            episodeId,
            "genre-fallback-ep",
            new DateTime(2026, 4, 10, 9, 0, 0, DateTimeKind.Utc),
            validatedTicks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var genreRow = await dbContext.UserGenrePeriodStats.SingleAsync(row =>
            row.UserId.Equals(userId)
            && row.PeriodType == PeriodType.Month
            && row.PeriodKey == "2026-04"
            && row.GenreId == "Drama");

        Assert.Equal(validatedTicks, genreRow.TotalValidatedTicks);
    }

    [Fact]
    public async Task MultiDeviceConcurrentSessions_AreTrackedIndependently()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Concurrent Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "session-a", 0, "Device-A"));
        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "session-b", 0, "Device-B"));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "session-a", TimeSpan.FromMinutes(5).Ticks, "Device-A"));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(user, movie, "session-b", TimeSpan.FromMinutes(5).Ticks, "Device-B"));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(user, movie, "session-a", TimeSpan.FromMinutes(10).Ticks, "Device-A"));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(user, movie, "session-b", TimeSpan.FromMinutes(10).Ticks, "Device-B"));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var sessions = await dbContext.UserWatchSessions
            .Where(session => session.UserId.Equals(userId))
            .ToListAsync();

        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, session => session.SessionId == "session-a");
        Assert.Contains(sessions, session => session.SessionId == "session-b");

        var monthStats = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03");
        Assert.Equal(2, monthStats.SessionCount);
        Assert.Equal(TimeSpan.FromMinutes(20).Ticks, monthStats.TotalValidatedTicks);
    }

    [Fact]
    public async Task AutomatedProgress_IsIgnoredForAnalyticsAndDoesNotInvalidateSession()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Automated Progress Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-auto", 0));
        for (var second = 1; second <= 120; second++)
        {
            context.TimeProvider.Advance(TimeSpan.FromSeconds(1));
            await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
                user,
                movie,
                "play-auto",
                TimeSpan.FromSeconds(second).Ticks,
                isAutomated: true));
        }

        context.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-auto",
            TimeSpan.FromMinutes(5).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(
            user,
            movie,
            "play-auto",
            TimeSpan.FromMinutes(10).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.IsValidSession);
        Assert.True(session.SuspicionScore <= 1);
        Assert.Equal(TimeSpan.FromMinutes(10).Ticks, session.ValidatedTicks);

        var monthStats = await dbContext.UserPeriodStats
            .SingleAsync(stats => stats.UserId.Equals(userId)
                && stats.PeriodType == PeriodType.Month
                && stats.PeriodKey == "2026-03");
        Assert.Equal(TimeSpan.FromMinutes(10).Ticks, monthStats.TotalValidatedTicks);
    }

    [Fact]
    public async Task ResumeOffset_FirstProgressEstablishesBaselineWithoutPenalty()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Resume Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-resume", 0));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(5));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-resume",
            TimeSpan.FromMinutes(20).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-resume",
            TimeSpan.FromMinutes(20).Ticks + TimeSpan.FromSeconds(10).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(
            user,
            movie,
            "play-resume",
            TimeSpan.FromMinutes(20).Ticks + TimeSpan.FromSeconds(20).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.IsValidSession);
        Assert.Equal(0, session.SuspicionScore);
        Assert.Equal(TimeSpan.FromSeconds(20).Ticks, session.ValidatedTicks);
    }

    [Fact]
    public async Task SparseFinalStopPosition_RecoversPlausibleValidatedTicks()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Sparse Stop Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-sparse-stop", 0));
        context.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-sparse-stop",
            TimeSpan.FromSeconds(5).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(5));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(
            user,
            movie,
            "play-sparse-stop",
            TimeSpan.FromMinutes(10).Ticks + TimeSpan.FromSeconds(5).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.IsValidSession);
        Assert.True(session.SuspicionScore >= 1);
        Assert.True(session.ValidatedTicks >= TimeSpan.FromMinutes(10).Ticks);
    }

    [Fact]
    public async Task SmallBackwardSeekJitter_DoesNotInvalidateSession()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var user = CreateUser(userId);
        var movie = new Movie
        {
            Id = itemId,
            Name = "Jitter Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 12, 30, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        await context.TrackingService.HandlePlaybackStart(CreateStartEvent(user, movie, "play-jitter", 0));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(30));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-jitter",
            TimeSpan.FromSeconds(30).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(5));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-jitter",
            TimeSpan.FromSeconds(28).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(5));
        await context.TrackingService.HandlePlaybackProgress(CreateProgressEvent(
            user,
            movie,
            "play-jitter",
            TimeSpan.FromSeconds(35).Ticks));
        context.TimeProvider.Advance(TimeSpan.FromSeconds(5));
        await context.TrackingService.HandlePlaybackStop(CreateStopEvent(
            user,
            movie,
            "play-jitter",
            TimeSpan.FromSeconds(40).Ticks));

        await using var dbContext = await context.DbFactory.CreateDbContextAsync();
        var session = await dbContext.UserWatchSessions.SingleAsync();
        Assert.True(session.IsValidSession);
        Assert.Equal(0, session.SuspicionScore);
        Assert.Equal(TimeSpan.FromSeconds(42).Ticks, session.ValidatedTicks);
    }

    [Fact]
    public async Task PersonalInsights_AllTimeInsightText_UsesOverallLabel()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "Insight Movie",
            RunTimeTicks = TimeSpan.FromHours(2).Ticks,
            Genres = ["Sci-Fi"]
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(movie);

        var validatedTicks = TimeSpan.FromMinutes(60).Ticks;
        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            itemId,
            "insight-session",
            new DateTime(2026, 3, 18, 9, 0, 0, DateTimeKind.Utc),
            validatedTicks,
            validatedTicks));

        var personalInsightsService = new PersonalInsightsService(context.DbFactory, context.TimeProvider);
        var result = await personalInsightsService.GetInsights(userId, PersonalInsightsPeriodType.AllTime);

        Assert.Equal("You've spent 100% of your time watching Sci-Fi overall.", result.InsightText);
    }

    [Fact]
    public async Task PersonalInsights_ContinueWatching_DeduplicatesByItemId()
    {
        var userId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();
        var firstMovie = new Movie
        {
            Id = itemId,
            Name = "Duplicate Candidate",
            RunTimeTicks = TimeSpan.FromMinutes(120).Ticks
        };
        var secondMovie = new Movie
        {
            Id = secondItemId,
            Name = "Second Candidate",
            RunTimeTicks = TimeSpan.FromMinutes(90).Ticks
        };

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(firstMovie);
        await context.RegisterItemAsync(secondMovie);

        var latestPlayed = new DateTime(2026, 3, 19, 9, 55, 0, DateTimeKind.Utc);
        await using (var dbContext = await context.DbFactory.CreateDbContextAsync())
        {
            dbContext.Users.Add(new Jellyfin.Database.Implementations.Entities.User("insights-user", "default", "default")
            {
                Id = userId
            });

            dbContext.UserData.AddRange(
                new UserData
                {
                    ItemId = itemId,
                    UserId = userId,
                    CustomDataKey = "external-key-a",
                    PlaybackPositionTicks = TimeSpan.FromMinutes(35).Ticks,
                    Played = false,
                    LastPlayedDate = latestPlayed,
                    Item = null!,
                    User = null!
                },
                new UserData
                {
                    ItemId = itemId,
                    UserId = userId,
                    CustomDataKey = itemId.ToString("D"),
                    PlaybackPositionTicks = TimeSpan.FromMinutes(35).Ticks,
                    Played = false,
                    LastPlayedDate = latestPlayed,
                    Item = null!,
                    User = null!
                },
                new UserData
                {
                    ItemId = itemId,
                    UserId = userId,
                    CustomDataKey = "external-key-b",
                    PlaybackPositionTicks = TimeSpan.FromMinutes(35).Ticks,
                    Played = false,
                    LastPlayedDate = latestPlayed,
                    Item = null!,
                    User = null!
                },
                new UserData
                {
                    ItemId = secondItemId,
                    UserId = userId,
                    CustomDataKey = secondItemId.ToString("D"),
                    PlaybackPositionTicks = TimeSpan.FromMinutes(10).Ticks,
                    Played = false,
                    LastPlayedDate = latestPlayed.AddMinutes(-10),
                    Item = null!,
                    User = null!
                });

            await dbContext.SaveChangesAsync();
        }

        var personalInsightsService = new PersonalInsightsService(context.DbFactory, context.TimeProvider);
        var result = await personalInsightsService.GetInsights(userId, PersonalInsightsPeriodType.Month);

        Assert.Equal(2, result.ContinueWatching.Count);
        Assert.Equal(itemId, result.ContinueWatching[0].ItemId);
        Assert.Single(result.ContinueWatching, row => row.ItemId.Equals(itemId));
        Assert.Contains(result.ContinueWatching, row => row.ItemId.Equals(secondItemId));
    }

    [Fact]
    public async Task PersonalInsights_CompletionCounts_AreSourcedFromAggregatedStats()
    {
        var userId = Guid.NewGuid();
        var monthMovieId = Guid.NewGuid();
        var previousMovieId = Guid.NewGuid();
        var monthEpisodeId = Guid.NewGuid();
        var previousEpisodeId = Guid.NewGuid();
        var userDataOnlyMovieId = Guid.NewGuid();

        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 3, 19, 10, 0, 0, TimeSpan.Zero));
        await context.RegisterItemAsync(new Movie
        {
            Id = monthMovieId,
            Name = "Month Movie",
            RunTimeTicks = TimeSpan.FromMinutes(100).Ticks
        });
        await context.RegisterItemAsync(new Movie
        {
            Id = previousMovieId,
            Name = "Previous Movie",
            RunTimeTicks = TimeSpan.FromMinutes(110).Ticks
        });
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = monthEpisodeId,
            Name = "Month Episode",
            ParentIndexNumber = 1,
            IndexNumber = 1,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });
        await context.RegisterItemAsync(new MediaBrowser.Controller.Entities.TV.Episode
        {
            Id = previousEpisodeId,
            Name = "Previous Episode",
            ParentIndexNumber = 1,
            IndexNumber = 2,
            RunTimeTicks = TimeSpan.FromMinutes(24).Ticks
        });
        await context.RegisterItemAsync(new Movie
        {
            Id = userDataOnlyMovieId,
            Name = "UserData Only Movie",
            RunTimeTicks = TimeSpan.FromMinutes(95).Ticks
        });

        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            previousMovieId,
            "prev-movie",
            new DateTime(2026, 2, 10, 8, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(110).Ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            previousEpisodeId,
            "prev-episode",
            new DateTime(2026, 2, 10, 10, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(24).Ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            monthMovieId,
            "month-movie",
            new DateTime(2026, 3, 12, 9, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(100).Ticks));
        await context.PersistAndAggregateAsync(CreateValidSession(
            userId,
            monthEpisodeId,
            "month-episode",
            new DateTime(2026, 3, 12, 11, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(24).Ticks));

        await using (var dbContext = await context.DbFactory.CreateDbContextAsync())
        {
            dbContext.Users.Add(new Jellyfin.Database.Implementations.Entities.User("completed-user", "default", "default")
            {
                Id = userId
            });

            dbContext.UserData.AddRange(
                new UserData
                {
                    ItemId = userDataOnlyMovieId,
                    UserId = userId,
                    CustomDataKey = userDataOnlyMovieId.ToString("D"),
                    PlaybackPositionTicks = 0,
                    Played = true,
                    LastPlayedDate = new DateTime(2026, 3, 12, 14, 0, 0, DateTimeKind.Utc),
                    Item = null!,
                    User = null!
                });

            await dbContext.SaveChangesAsync();
        }

        var personalInsightsService = new PersonalInsightsService(context.DbFactory, context.TimeProvider);
        var result = await personalInsightsService.GetInsights(userId, PersonalInsightsPeriodType.Month);

        Assert.Equal(1, result.Summary.MoviesWatched);
        Assert.Equal(0, result.Summary.MoviesDelta);
        Assert.Equal(1, result.Summary.EpisodesWatched);
        Assert.Equal(0, result.Summary.EpisodesDelta);
        Assert.True(result.Summary.TotalWatchHours > 2D);
    }

    [Fact]
    public async Task PersonalInsights_NoActivityPeriod_DoesNotReportPeakViewingActivity()
    {
        var userId = Guid.NewGuid();
        await using var context = await CreateContextAsync(new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero));

        var personalInsightsService = new PersonalInsightsService(context.DbFactory, context.TimeProvider);
        var monthResult = await personalInsightsService.GetInsights(userId, PersonalInsightsPeriodType.Month);

        Assert.Equal(0, monthResult.Summary.TotalWatchHours);
        Assert.False(monthResult.PeakViewing.HasViewingActivity);
        Assert.Equal("No activity yet", monthResult.PeakViewing.Label);
        Assert.All(monthResult.PeakViewing.HourlyDistribution, hour => Assert.Equal(0, hour.Minutes));
    }

    private static User CreateUser(Guid userId)
        => new("test-user", "default", "default")
        {
            Id = userId
        };

    private static PlaybackStartEventArgs CreateStartEvent(User user, BaseItem item, string playSessionId, long positionTicks, string deviceName = "Device")
        => new()
        {
            Users = new List<User> { user },
            Item = item,
            MediaInfo = new BaseItemDto
            {
                Id = item.Id,
                Name = item.Name
            },
            PlaybackPositionTicks = positionTicks,
            PlaySessionId = playSessionId,
            DeviceId = deviceName,
            DeviceName = deviceName,
            ClientName = "TestClient"
        };

    private static PlaybackProgressEventArgs CreateProgressEvent(
        User user,
        BaseItem item,
        string playSessionId,
        long positionTicks,
        string deviceName = "Device",
        bool isAutomated = false)
        => new()
        {
            Users = new List<User> { user },
            Item = item,
            MediaInfo = new BaseItemDto
            {
                Id = item.Id,
                Name = item.Name
            },
            PlaybackPositionTicks = positionTicks,
            PlaySessionId = playSessionId,
            DeviceId = deviceName,
            DeviceName = deviceName,
            ClientName = "TestClient",
            IsPaused = false,
            IsAutomated = isAutomated
        };

    private static PlaybackStopEventArgs CreateStopEvent(User user, BaseItem item, string playSessionId, long positionTicks, string deviceName = "Device")
        => new()
        {
            Users = new List<User> { user },
            Item = item,
            MediaInfo = new BaseItemDto
            {
                Id = item.Id,
                Name = item.Name
            },
            PlaybackPositionTicks = positionTicks,
            PlaySessionId = playSessionId,
            DeviceId = deviceName,
            DeviceName = deviceName,
            ClientName = "TestClient"
        };

    private static UserWatchSession CreateValidSession(
        Guid userId,
        Guid itemId,
        string sessionId,
        DateTime startTimeUtc,
        long validatedTicks,
        long? accumulatedTicks = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ItemId = itemId,
            SessionId = sessionId,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = startTimeUtc.AddTicks(validatedTicks),
            AccumulatedTicks = accumulatedTicks ?? validatedTicks,
            ValidatedTicks = validatedTicks,
            PlaybackSpeed = 1D,
            IsValidSession = true,
            SuspicionScore = 0
        };

    private static async Task<TrackingTestContext> CreateContextAsync(DateTimeOffset nowUtc)
    {
        var context = new TrackingTestContext(nowUtc);
        await context.InitializeAsync();
        return context;
    }

    private sealed class TrackingTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestDatabaseProvider _databaseProvider;
        private readonly Dictionary<Guid, BaseItem> _items = new();

        public TrackingTestContext(DateTimeOffset nowUtc)
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<JellyfinDbContext>()
                .UseSqlite(_connection)
                .Options;

            _databaseProvider = new TestDatabaseProvider();
            DbFactory = new TestDbContextFactory(options, _databaseProvider);
            TimeProvider = new ManualTimeProvider(nowUtc);
            LibraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
            LibraryManager
                .Setup(manager => manager.GetItemById(It.IsAny<Guid>()))
                .Returns<Guid>(id => _items.TryGetValue(id, out var item) ? item : null);
            AggregationService = new WatchSessionAggregationService(DbFactory, null, null, LibraryManager.Object, NullLogger<WatchSessionAggregationService>.Instance);
            TrackingService = new WatchSessionTrackingService(DbFactory, AggregationService, TimeProvider, NullLogger<WatchSessionTrackingService>.Instance);
        }

        public TestDbContextFactory DbFactory { get; }

        public ManualTimeProvider TimeProvider { get; }

        public Mock<ILibraryManager> LibraryManager { get; }

        public WatchSessionAggregationService AggregationService { get; }

        public WatchSessionTrackingService TrackingService { get; }

        public async Task InitializeAsync()
        {
            await using var dbContext = await DbFactory.CreateDbContextAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }

        public async Task RegisterItemAsync(BaseItem item)
        {
            _items[item.Id] = item;
            await using var dbContext = await DbFactory.CreateDbContextAsync();
            var existing = await dbContext.BaseItems.FirstOrDefaultAsync(baseItem => baseItem.Id.Equals(item.Id));
            if (existing is null)
            {
                dbContext.BaseItems.Add(ToEntity(item));
            }
            else
            {
                existing.Name = item.Name;
                existing.RunTimeTicks = item.RunTimeTicks;
                existing.Type = item.GetType().Name;
                existing.SeriesId = item is MediaBrowser.Controller.Entities.TV.Episode episode && !episode.SeriesId.Equals(Guid.Empty)
                    ? episode.SeriesId
                    : null;
                existing.ParentIndexNumber = item.ParentIndexNumber;
                existing.IndexNumber = item.IndexNumber;
            }

            await dbContext.SaveChangesAsync();
        }

        public async Task PersistAndAggregateAsync(UserWatchSession session)
        {
            await using var dbContext = await DbFactory.CreateDbContextAsync();
            dbContext.UserWatchSessions.Add(session);
            await dbContext.SaveChangesAsync();
            await AggregationService.ProcessSession(session);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.DisposeAsync();
        }

        private static BaseItemEntity ToEntity(BaseItem item)
            => new()
            {
                Id = item.Id,
                Type = item.GetType().Name,
                Name = item.Name,
                RunTimeTicks = item.RunTimeTicks,
                SeriesId = item is MediaBrowser.Controller.Entities.TV.Episode episode && !episode.SeriesId.Equals(Guid.Empty)
                    ? episode.SeriesId
                    : null,
                ParentIndexNumber = item.ParentIndexNumber,
                IndexNumber = item.IndexNumber,
                IsFolder = item.IsFolder,
                IsVirtualItem = item.IsVirtualItem,
                IsInMixedFolder = item.IsInMixedFolder,
                IsLocked = item.IsLocked,
                IsMovie = item is Movie,
                IsRepeat = false,
                IsSeries = item is MediaBrowser.Controller.Entities.TV.Series
            };
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
            => _utcNow;

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }
    }

    private sealed class TestDbContextFactory : IDbContextFactory<JellyfinDbContext>
    {
        private readonly DbContextOptions<JellyfinDbContext> _options;
        private readonly TestDatabaseProvider _databaseProvider;

        public TestDbContextFactory(DbContextOptions<JellyfinDbContext> options, TestDatabaseProvider databaseProvider)
        {
            _options = options;
            _databaseProvider = databaseProvider;
        }

        public JellyfinDbContext CreateDbContext()
            => new(
                _options,
                NullLogger<JellyfinDbContext>.Instance,
                _databaseProvider,
                new NoLockBehavior(NullLogger<NoLockBehavior>.Instance));

        public Task<JellyfinDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class TestDatabaseProvider : IJellyfinDatabaseProvider
    {
        public IDbContextFactory<JellyfinDbContext>? DbContextFactory { get; set; }

        public void Initialise(DbContextOptionsBuilder options, DatabaseConfigurationOptions databaseConfiguration)
        {
        }

        public void OnModelCreating(ModelBuilder modelBuilder)
        {
        }

        public void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
        }

        public Task RunScheduledOptimisation(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task RunShutdownTask(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<string> MigrationBackupFast(CancellationToken cancellationToken)
            => Task.FromResult(string.Empty);

        public Task RestoreBackupFast(string key, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DeleteBackup(string key)
            => Task.CompletedTask;

        public Task PurgeDatabase(JellyfinDbContext dbContext, IEnumerable<string>? tableNames)
            => Task.CompletedTask;
    }
}
