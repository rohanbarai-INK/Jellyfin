using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Jellyfin.Api.Models.ContentRequestDtos;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Server.Integration.Tests.Controllers
{
    public class ContentRequestControllerTests : IClassFixture<JellyfinApplicationFactory>
    {
        private readonly JellyfinApplicationFactory _factory;
        private static string? _adminAccessToken;

        public ContentRequestControllerTests(JellyfinApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CreateRequest_EnforcesMovieCapWithinCycle()
        {
            var (_, userClient) = await CreateActiveUserClient();

            for (var index = 0; index < 5; index++)
            {
                using var response = await userClient.PostAsJsonAsync(
                    "Request",
                    new CreateContentRequestRequest
                    {
                        Title = $"Movie Cap {Guid.NewGuid():N}",
                        Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                    },
                    JsonDefaults.Options);

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using var overflowResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Movie Cap Overflow {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.Conflict, overflowResponse.StatusCode);
        }

        [Fact]
        public async Task CreateRequest_AllowsQuotaTopUpWithServerCoinBalance()
        {
            var (user, userClient) = await CreateActiveUserClient();
            await SeedAchievementCoins(user.Id, 500);

            for (var index = 0; index < 5; index++)
            {
                await InsertRequest(
                    user.Id,
                    $"Movie Quota Exhaust {index} {Guid.NewGuid():N}",
                    ContentRequestType.Movie,
                    ContentRequestStatus.Pending,
                    DateTime.UtcNow.AddMinutes(-index));
            }

            using var response = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Movie Top Up {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var createdRow = await response.Content.ReadFromJsonAsync<ContentRequestRowDto>(JsonDefaults.Options);
            Assert.NotNull(createdRow);

            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var createdEntity = await dbContext.ContentRequests.FirstAsync(row => row.Id.Equals(createdRow.Id));
            Assert.Equal(200, createdEntity.CoinRedeemCost);
        }

        [Fact]
        public async Task CreateRequest_QuotaTopUpDeductsCoinsAndBlocksWhenBalanceRunsOut()
        {
            var (user, userClient) = await CreateActiveUserClient();
            await SeedAchievementCoins(user.Id, 200);

            for (var index = 0; index < 5; index++)
            {
                await InsertRequest(
                    user.Id,
                    $"Quota Exhausted Movie {index} {Guid.NewGuid():N}",
                    ContentRequestType.Movie,
                    ContentRequestStatus.Pending,
                    DateTime.UtcNow.AddMinutes(-index));
            }

            using var firstTopUp = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Top Up First {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, firstTopUp.StatusCode);

            using var secondTopUp = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Top Up Second {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.Conflict, secondTopUp.StatusCode);
        }

        [Fact]
        public async Task CreateRequest_BlocksDuplicatesForPendingAndApproved()
        {
            var (user, userClient) = await CreateActiveUserClient();
            var adminClient = await CreateAdminClient();

            var createPayload = new CreateContentRequestRequest
            {
                Title = $"Duplicate Check {Guid.NewGuid():N}",
                Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
            };

            using var createResponse = await userClient.PostAsJsonAsync("Request", createPayload, JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var createdRow = await createResponse.Content.ReadFromJsonAsync<ContentRequestRowDto>(JsonDefaults.Options);
            Assert.NotNull(createdRow);

            using var approveResponse = await adminClient.PostAsJsonAsync(
                "Request/Admin/Approve",
                new AdminRequestActionRequest
                {
                    RequestId = createdRow.Id
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

            using var duplicateResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"  {createPayload.Title.ToUpperInvariant()}   ",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        }

        [Fact]
        public async Task CreateRequest_IgnoresPreviousCycleRowsForCap()
        {
            var (user, userClient) = await CreateActiveUserClient();
            var now = DateTime.UtcNow;
            var subscriptionStartDate = now.AddMonths(-1).AddDays(-3);
            var currentCycleStart = GetCurrentCycleStart(subscriptionStartDate, now);

            await InsertRedeemedKey(user.Id, subscriptionStartDate, now.AddMonths(1));

            for (var index = 0; index < 5; index++)
            {
                await InsertRequest(
                    user.Id,
                    $"Previous Cycle Movie {index} {Guid.NewGuid():N}",
                    ContentRequestType.Movie,
                    ContentRequestStatus.Pending,
                    currentCycleStart.AddMinutes(-5 - index));
            }

            using var response = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Current Cycle Movie {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateRequest_RenewAfterExpiry_ResetsCycleAnchorToLatestRedemption()
        {
            var (user, userClient) = await CreateActiveUserClient();

            var now = DateTime.UtcNow;
            var firstRedeemAt = now.AddMonths(-3).AddDays(-10);
            var secondRedeemAt = now.AddDays(-2);
            var secondExpiryDate = secondRedeemAt.AddMonths(1);

            await InsertRedeemedKey(user.Id, firstRedeemAt, firstRedeemAt.AddMonths(1));
            await InsertRedeemedKey(user.Id, secondRedeemAt, secondExpiryDate);

            var oldCycleStart = GetCurrentCycleStart(firstRedeemAt, now);
            Assert.True(oldCycleStart.AddMinutes(5) < secondRedeemAt);

            for (var index = 0; index < 5; index++)
            {
                await InsertRequest(
                    user.Id,
                    $"Lapsed Cycle Movie {index} {Guid.NewGuid():N}",
                    ContentRequestType.Movie,
                    ContentRequestStatus.Pending,
                    oldCycleStart.AddMinutes(index + 1));
            }

            using var response = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Renewed Cycle Movie {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateRequest_CompletedRowsCountTowardsMovieCapWithinCycle()
        {
            var (user, userClient) = await CreateActiveUserClient();

            for (var index = 0; index < 5; index++)
            {
                await InsertRequest(
                    user.Id,
                    $"Completed Cap Movie {index} {Guid.NewGuid():N}",
                    ContentRequestType.Movie,
                    ContentRequestStatus.Completed,
                    DateTime.UtcNow.AddMinutes(-index),
                    jellyfinItemId: Guid.NewGuid());
            }

            using var overflowResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Completed Cap Overflow {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.Conflict, overflowResponse.StatusCode);
        }

        [Fact]
        public async Task GetMyRequests_QuotaCountsCompletedRowsInUsage()
        {
            var (user, userClient) = await CreateActiveUserClient();

            await InsertRequest(
                user.Id,
                $"Completed Quota Movie {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Completed,
                DateTime.UtcNow,
                jellyfinItemId: Guid.NewGuid());

            await InsertRequest(
                user.Id,
                $"Completed Quota Series {Guid.NewGuid():N}",
                ContentRequestType.Series,
                ContentRequestStatus.Completed,
                DateTime.UtcNow,
                seasonNumber: 1,
                jellyfinItemId: Guid.NewGuid());

            using var response = await userClient.GetAsync("Request/My");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<MyContentRequestsResponse>(JsonDefaults.Options);
            Assert.NotNull(payload);
            Assert.NotNull(payload.Quota);

            Assert.Equal(1, payload.Quota.UsedMovies);
            Assert.Equal(1, payload.Quota.UsedSeries);
            Assert.Equal(4, payload.Quota.RemainingMovies);
            Assert.Equal(1, payload.Quota.RemainingSeries);
        }

        [Fact]
        public async Task PublicRequests_ExcludeRejectedRows()
        {
            var (_, userClient) = await CreateActiveUserClient();
            var adminClient = await CreateAdminClient();

            using var pendingCreateResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Public Pending {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, pendingCreateResponse.StatusCode);
            var pendingRow = await pendingCreateResponse.Content.ReadFromJsonAsync<ContentRequestRowDto>(JsonDefaults.Options);
            Assert.NotNull(pendingRow);

            using var rejectedCreateResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Public Rejected {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, rejectedCreateResponse.StatusCode);
            var rejectedRow = await rejectedCreateResponse.Content.ReadFromJsonAsync<ContentRequestRowDto>(JsonDefaults.Options);
            Assert.NotNull(rejectedRow);

            using var rejectResponse = await adminClient.PostAsJsonAsync(
                "Request/Admin/Reject",
                new AdminRequestActionRequest
                {
                    RequestId = rejectedRow.Id
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

            using var publicResponse = await userClient.GetAsync("Request/Public?skip=0&take=100");
            Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
            var publicRows = await publicResponse.Content.ReadFromJsonAsync<PublicContentRequestListResponse>(JsonDefaults.Options);
            Assert.NotNull(publicRows);

            Assert.Contains(publicRows.Items, row => row.Id.Equals(pendingRow.Id));
            Assert.DoesNotContain(publicRows.Items, row => row.Id.Equals(rejectedRow.Id));
        }

        [Fact]
        public async Task Notifications_ReturnOnlyCompletedRowsWithNotificationCountLowerThanTwo()
        {
            var (user, userClient) = await CreateActiveUserClient();
            var adminClient = await CreateAdminClient();

            using var createResponse = await userClient.PostAsJsonAsync(
                "Request",
                new CreateContentRequestRequest
                {
                    Title = $"Notification Included {Guid.NewGuid():N}",
                    Type = MediaBrowser.Controller.ContentRequests.ContentRequestType.Movie
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var createdRow = await createResponse.Content.ReadFromJsonAsync<ContentRequestRowDto>(JsonDefaults.Options);
            Assert.NotNull(createdRow);

            using var approveResponse = await adminClient.PostAsJsonAsync(
                "Request/Admin/Approve",
                new AdminRequestActionRequest
                {
                    RequestId = createdRow.Id
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

            using var completeResponse = await adminClient.PostAsJsonAsync(
                "Request/Admin/Complete",
                new AdminCompleteContentRequestRequest
                {
                    RequestId = createdRow.Id,
                    JellyfinItemId = Guid.NewGuid()
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

            var hiddenCompletedId = await InsertRequest(
                user.Id,
                $"Notification Hidden {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Completed,
                DateTime.UtcNow,
                jellyfinItemId: Guid.NewGuid(),
                notificationCount: 2);

            await InsertRequest(
                user.Id,
                $"Notification Rejected {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Rejected,
                DateTime.UtcNow);

            using var notificationsResponse = await userClient.GetAsync("Request/Notifications");
            Assert.Equal(HttpStatusCode.OK, notificationsResponse.StatusCode);
            var notifications = await notificationsResponse.Content.ReadFromJsonAsync<List<ContentRequestRowDto>>(JsonDefaults.Options);
            Assert.NotNull(notifications);

            Assert.Contains(notifications, row => row.Id.Equals(createdRow.Id));
            Assert.DoesNotContain(notifications, row => row.Id.Equals(hiddenCompletedId));
        }

        [Fact]
        public async Task BulkNotificationViewed_IncrementsOwnedRowsOnly()
        {
            var (userA, userAClient) = await CreateActiveUserClient();
            var (userB, _) = await CreateActiveUserClient();

            var userARequestId = await InsertRequest(
                userA.Id,
                $"User A Completed {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Completed,
                DateTime.UtcNow,
                jellyfinItemId: Guid.NewGuid(),
                notificationCount: 0);

            var userBRequestId = await InsertRequest(
                userB.Id,
                $"User B Completed {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Completed,
                DateTime.UtcNow,
                jellyfinItemId: Guid.NewGuid(),
                notificationCount: 0);

            using var response = await userAClient.PostAsJsonAsync(
                "Request/NotificationViewedBulk",
                new BulkNotificationViewedRequest
                {
                    RequestIds = new[]
                    {
                        userARequestId,
                        userBRequestId,
                        Guid.NewGuid()
                    }
                },
                JsonDefaults.Options);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var refreshedA = await dbContext.ContentRequests.FirstAsync(row => row.Id.Equals(userARequestId));
            var refreshedB = await dbContext.ContentRequests.FirstAsync(row => row.Id.Equals(userBRequestId));

            Assert.Equal(1, refreshedA.NotificationCount);
            Assert.Equal(0, refreshedB.NotificationCount);
        }

        [Fact]
        public async Task GetAdminRequests_MarksUnseenPendingRowsAsViewed()
        {
            var (user, _) = await CreateActiveUserClient();
            var adminClient = await CreateAdminClient();

            var requestId = await InsertRequest(
                user.Id,
                $"Admin View Pending {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Pending,
                DateTime.UtcNow,
                isAdminViewed: false);

            using var adminResponse = await adminClient.GetAsync("Request/Admin");
            Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);

            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var refreshedRequest = await dbContext.ContentRequests
                .FirstAsync(row => row.Id.Equals(requestId))
                ;

            Assert.True(refreshedRequest.IsAdminViewed);
        }

        [Fact]
        public async Task AdminUnseenPendingCount_ChangesAfterAdminFetch()
        {
            var (user, _) = await CreateActiveUserClient();
            var adminClient = await CreateAdminClient();

            await InsertRequest(
                user.Id,
                $"Unseen Count Pending {Guid.NewGuid():N}",
                ContentRequestType.Movie,
                ContentRequestStatus.Pending,
                DateTime.UtcNow,
                isAdminViewed: false);

            using var beforeResponse = await adminClient.GetAsync("Request/Admin/UnseenPendingCount");
            Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
            var beforePayload = await beforeResponse.Content.ReadFromJsonAsync<AdminUnseenPendingCountResponse>(JsonDefaults.Options);
            Assert.NotNull(beforePayload);
            Assert.True(beforePayload.Count > 0);

            using var adminRowsResponse = await adminClient.GetAsync("Request/Admin");
            Assert.Equal(HttpStatusCode.OK, adminRowsResponse.StatusCode);

            using var afterResponse = await adminClient.GetAsync("Request/Admin/UnseenPendingCount");
            Assert.Equal(HttpStatusCode.OK, afterResponse.StatusCode);
            var afterPayload = await afterResponse.Content.ReadFromJsonAsync<AdminUnseenPendingCountResponse>(JsonDefaults.Options);
            Assert.NotNull(afterPayload);
            Assert.True(afterPayload.Count < beforePayload.Count);
        }

        private async Task<(UserDto User, HttpClient Client)> CreateActiveUserClient()
        {
            var adminClient = await CreateAdminClient();
            var username = $"request-user-{Guid.NewGuid():N}";
            var password = $"StrongPass-{Guid.NewGuid():N}!";
            var createdUser = await CreateUser(adminClient, username, password);

            var now = DateTime.UtcNow;
            await SetUserExpiryDate(createdUser.Id, now.AddMonths(2));
            await InsertRedeemedKey(createdUser.Id, now.AddDays(-1), now.AddMonths(2));

            var authResult = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(authResult.AccessToken);
            return (createdUser, userClient);
        }

        private async Task<HttpClient> CreateAdminClient()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(client));
            return client;
        }

        private async Task<UserDto> CreateUser(HttpClient adminClient, string username, string password)
        {
            using var createResponse = await adminClient.PostAsJsonAsync(
                "Users/New",
                new CreateUserByName
                {
                    Name = username,
                    Password = password
                },
                JsonDefaults.Options);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(JsonDefaults.Options);
            Assert.NotNull(createdUser);
            return createdUser;
        }

        private async Task SetUserExpiryDate(Guid userId, DateTime? expiryDate)
        {
            var userManager = _factory.Services.GetRequiredService<IUserManager>();
            var user = userManager.GetUserById(userId);
            Assert.NotNull(user);

            user.ExpiryDate = expiryDate;
            await userManager.UpdateUserAsync(user);
        }

        private async Task<AuthenticationResultDto> AuthenticateByName(HttpClient client, string username, string password)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/Users/AuthenticateByName");
            request.Headers.TryAddWithoutValidation(AuthHelper.AuthHeaderName, AuthHelper.DummyAuthHeader);
            request.Content = JsonContent.Create(
                new AuthenticateUserByName
                {
                    Username = username,
                    Pw = password
                },
                options: JsonDefaults.Options);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var authResult = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>(JsonDefaults.Options);
            Assert.NotNull(authResult);
            return authResult;
        }

        private async Task InsertRedeemedKey(Guid userId, DateTime redeemedAt, DateTime expiryDate)
        {
            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var user = await dbContext.Users.FirstAsync(dbUser => dbUser.Id.Equals(userId));
            user.ExpiryDate = expiryDate;

            var keyValue = $"TEST-{Guid.NewGuid():N}".Substring(0, 32);
            var accessKey = new AccessKey(keyValue, 1)
            {
                IsRedeemed = true,
                RedeemedByUserId = userId,
                RedeemedAt = redeemedAt,
                CreatedAt = redeemedAt.AddMinutes(-1)
            };

            dbContext.AccessKeys.Add(accessKey);
            await dbContext.SaveChangesAsync();
        }

        private async Task<Guid> InsertRequest(
            Guid userId,
            string title,
            ContentRequestType type,
            ContentRequestStatus status,
            DateTime requestedAt,
            int? seasonNumber = null,
            Guid? jellyfinItemId = null,
            int notificationCount = 0,
            bool isAdminViewed = false,
            int coinRedeemCost = 0)
        {
            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var requestEntity = new ContentRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = title,
                NormalizedTitle = title.Trim().ToLowerInvariant(),
                Type = type,
                SeasonNumber = seasonNumber,
                RequestedAt = requestedAt,
                Status = status,
                JellyfinItemId = jellyfinItemId,
                NotificationCount = notificationCount,
                IsAdminViewed = isAdminViewed,
                CoinRedeemCost = coinRedeemCost
            };

            dbContext.ContentRequests.Add(requestEntity);
            await dbContext.SaveChangesAsync();
            return requestEntity.Id;
        }

        private async Task SeedAchievementCoins(Guid userId, int coins)
        {
            var dbContextFactory = _factory.Services.GetRequiredService<IDbContextFactory<JellyfinDbContext>>();
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();

            var achievementId = $"server-coin-balance-{Guid.NewGuid():N}";
            dbContext.AchievementDefinitions.Add(new AchievementDefinition
            {
                Id = achievementId,
                Title = "Server Coin Seed",
                Description = "Seeded server coin balance for integration test.",
                ImageEmoji = "S",
                Rarity = "legendary",
                Xp = 0,
                Coins = Math.Max(0, coins),
                IsSeasonal = false
            });

            dbContext.UserAchievements.Add(new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievementId,
                UnlockedAtUtc = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync();
        }

        private static async Task<string> GetAdminAccessToken(HttpClient client)
        {
            _adminAccessToken ??= await AuthHelper.CompleteStartupAsync(client);
            return _adminAccessToken;
        }

        private static DateTime GetCurrentCycleStart(DateTime subscriptionStartDate, DateTime now)
        {
            var fullMonthsElapsed = ((now.Year - subscriptionStartDate.Year) * 12) + now.Month - subscriptionStartDate.Month;
            var candidateCycleStart = subscriptionStartDate.AddMonths(fullMonthsElapsed);

            if (candidateCycleStart > now)
            {
                candidateCycleStart = subscriptionStartDate.AddMonths(fullMonthsElapsed - 1);
            }

            while (candidateCycleStart.AddMonths(1) <= now)
            {
                candidateCycleStart = candidateCycleStart.AddMonths(1);
            }

            return candidateCycleStart;
        }

        private sealed class AuthenticationResultDto
        {
            public string AccessToken { get; set; } = string.Empty;

            public UserDto User { get; set; } = new();
        }
    }
}
