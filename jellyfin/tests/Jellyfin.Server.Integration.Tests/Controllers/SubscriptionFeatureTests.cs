using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Api.Models.AccessKeyDtos;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Priority;

namespace Jellyfin.Server.Integration.Tests.Controllers
{
    [TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
    public class SubscriptionFeatureTests : IClassFixture<JellyfinApplicationFactory>
    {
        private readonly JellyfinApplicationFactory _factory;
        private readonly JsonSerializerOptions _jsonOptions = JsonDefaults.Options;
        private static string? _adminAccessToken;

        public SubscriptionFeatureTests(JellyfinApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        [Priority(-1)]
        public async Task SubscriptionConfiguration_DefaultsAreReturned()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(client));

            using var response = await client.GetAsync("System/Configuration/subscription");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var config = await response.Content.ReadFromJsonAsync<SubscriptionConfiguration>(_jsonOptions);
            var defaultConfig = new SubscriptionConfiguration();
            Assert.NotNull(config);
            Assert.Equal(defaultConfig.BasePricePerMonth, config.BasePricePerMonth);
            Assert.Equal(defaultConfig.OneMonthPrice, config.OneMonthPrice);
            Assert.Equal(defaultConfig.ThreeMonthPrice, config.ThreeMonthPrice);
            Assert.Equal(defaultConfig.SixMonthPrice, config.SixMonthPrice);
            Assert.Equal(defaultConfig.TwelveMonthPrice, config.TwelveMonthPrice);
            AssertPlanBreakdown(config);
        }

        [Fact]
        [Priority(0)]
        public async Task SubscriptionConfiguration_CanBeUpdated()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(client));

            var updatedConfig = CreateUpdatedPricing();

            using var updateResponse = await client.PostAsJsonAsync(
                "System/Configuration/subscription",
                updatedConfig,
                _jsonOptions);
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            using var getResponse = await client.GetAsync("System/Configuration/subscription");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var config = await getResponse.Content.ReadFromJsonAsync<SubscriptionConfiguration>(_jsonOptions);
            Assert.NotNull(config);
            Assert.Equal(updatedConfig.BasePricePerMonth, config.BasePricePerMonth);
            Assert.Equal(updatedConfig.OneMonthPrice, config.OneMonthPrice);
            Assert.Equal(updatedConfig.ThreeMonthPrice, config.ThreeMonthPrice);
            Assert.Equal(updatedConfig.SixMonthPrice, config.SixMonthPrice);
            Assert.Equal(updatedConfig.TwelveMonthPrice, config.TwelveMonthPrice);
            AssertPlanBreakdown(config);
        }

        [Fact]
        [Priority(1)]
        public async Task ExpiredUser_CanAuthenticateAndReturnsExpiredStatus()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"expired-auth-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);
            await SetUserExpiryDate(createdUser.Id, DateTime.UtcNow.AddDays(-1));

            var authResult = await AuthenticateByName(_factory.CreateClient(), username, password);
            Assert.False(string.IsNullOrWhiteSpace(authResult.AccessToken));
            Assert.Equal("Expired", authResult.User.Status);
        }

        [Fact]
        [Priority(2)]
        public async Task ExpiredNonAdminUser_IsRestrictedExceptWhitelistedEndpoints()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"expired-user-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);
            await SetUserExpiryDate(createdUser.Id, DateTime.UtcNow.AddDays(-1));

            var userAuth = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(userAuth.AccessToken);

            using var deniedResponse = await userClient.GetAsync($"Users/{createdUser.Id:N}/Items/Root");
            Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
            var deniedPayload = await deniedResponse.Content.ReadAsStringAsync();
            Assert.Contains("SubscriptionExpired", deniedPayload, StringComparison.OrdinalIgnoreCase);

            using var meResponse = await userClient.GetAsync("Users/Me");
            Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

            using var pricingResponse = await userClient.GetAsync("System/Configuration/subscription");
            Assert.Equal(HttpStatusCode.OK, pricingResponse.StatusCode);

            using var keyResponse = await adminClient.PostAsJsonAsync(
                "Keys/Generate",
                new GenerateAccessKeyRequest
                {
                    DurationMonths = 1
                },
                _jsonOptions);
            Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);

            var generatedKey = await keyResponse.Content.ReadFromJsonAsync<GenerateAccessKeyResponse>(_jsonOptions);
            Assert.NotNull(generatedKey);

            using var redeemResponse = await userClient.PostAsJsonAsync(
                "Keys/Redeem",
                new RedeemAccessKeyRequest
                {
                    Key = generatedKey.Key
                },
                _jsonOptions);
            Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);

            using var logoutResponse = await userClient.PostAsync(
                "Sessions/Logout",
                new ByteArrayContent(Array.Empty<byte>()));
            Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        }

        [Fact]
        [Priority(3)]
        public async Task ExpiredNonAdminUser_CanAccessAuthenticateByNameEndpoint()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"expired-authcheck-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);
            await SetUserExpiryDate(createdUser.Id, DateTime.UtcNow.AddDays(-1));

            var userAuth = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(userAuth.AccessToken);

            using var authenticateResponse = await userClient.PostAsJsonAsync(
                "Users/AuthenticateByName",
                new AuthenticateUserByName
                {
                    Username = username,
                    Pw = password
                },
                _jsonOptions);
            Assert.NotEqual(HttpStatusCode.Forbidden, authenticateResponse.StatusCode);
        }

        [Fact]
        [Priority(4)]
        public async Task ExpiredNonAdminUser_CanAccessLoginBootstrapEndpoints()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"expired-bootstrap-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);
            await SetUserExpiryDate(createdUser.Id, DateTime.UtcNow.AddDays(-1));

            var userAuth = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(userAuth.AccessToken);

            using var displayPreferencesResponse = await userClient.GetAsync($"DisplayPreferences/usersettings?userId={createdUser.Id:N}&client=emby");
            Assert.NotEqual(HttpStatusCode.Forbidden, displayPreferencesResponse.StatusCode);

            using var endpointResponse = await userClient.GetAsync("System/Endpoint");
            Assert.NotEqual(HttpStatusCode.Forbidden, endpointResponse.StatusCode);

            using var bitrateResponse = await userClient.GetAsync("Playback/BitrateTest?Size=500000");
            Assert.NotEqual(HttpStatusCode.Forbidden, bitrateResponse.StatusCode);

            using var currentUserResponse = await userClient.GetAsync($"Users/{createdUser.Id:N}");
            Assert.NotEqual(HttpStatusCode.Forbidden, currentUserResponse.StatusCode);
        }

        [Fact]
        [Priority(5)]
        public async Task ExpiredAdminUser_IsNotRestricted()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(client));

            var adminUser = await AuthHelper.GetUserDtoAsync(client);
            await SetUserExpiryDate(adminUser.Id, DateTime.UtcNow.AddDays(-1));

            using var response = await client.GetAsync($"Users/{adminUser.Id:N}/Items/Root");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Priority(6)]
        public async Task CurrentSubscription_ReturnsActiveStatusWithoutHistory()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"active-current-sub-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);

            var userAuth = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(userAuth.AccessToken);

            using var response = await userClient.GetAsync("Keys/CurrentSubscription");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var payload = await response.Content.ReadFromJsonAsync<CurrentSubscriptionResponse>(_jsonOptions);
            Assert.NotNull(payload);
            Assert.Equal("Active", payload.Status);
            Assert.Null(payload.ExpiryDate);
            Assert.Null(payload.LastDurationMonths);
            Assert.Null(payload.LastRedeemedAt);

            using var userResponse = await userClient.GetAsync("Users/Me");
            Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
            var userDto = await userResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
            Assert.NotNull(userDto);
            Assert.Equal(createdUser.Id, userDto.Id);
        }

        [Fact]
        [Priority(7)]
        public async Task CurrentSubscription_ReturnsLatestRedeemedPlan()
        {
            var adminClient = _factory.CreateClient();
            adminClient.DefaultRequestHeaders.AddAuthHeader(await GetAdminAccessToken(adminClient));

            var username = $"redeemed-current-sub-{Guid.NewGuid():N}";
            var password = CreateTestPassword();
            var createdUser = await CreateUser(adminClient, username, password);
            await SetUserExpiryDate(createdUser.Id, DateTime.UtcNow.AddDays(-1));

            var userAuth = await AuthenticateByName(_factory.CreateClient(), username, password);
            var userClient = _factory.CreateClient();
            userClient.DefaultRequestHeaders.AddAuthHeader(userAuth.AccessToken);

            using var keyResponse = await adminClient.PostAsJsonAsync(
                "Keys/Generate",
                new GenerateAccessKeyRequest
                {
                    DurationMonths = 6
                },
                _jsonOptions);
            Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);

            var generatedKey = await keyResponse.Content.ReadFromJsonAsync<GenerateAccessKeyResponse>(_jsonOptions);
            Assert.NotNull(generatedKey);

            using var redeemResponse = await userClient.PostAsJsonAsync(
                "Keys/Redeem",
                new RedeemAccessKeyRequest
                {
                    Key = generatedKey.Key
                },
                _jsonOptions);
            Assert.Equal(HttpStatusCode.OK, redeemResponse.StatusCode);

            using var currentSubscriptionResponse = await userClient.GetAsync("Keys/CurrentSubscription");
            Assert.Equal(HttpStatusCode.OK, currentSubscriptionResponse.StatusCode);

            var payload = await currentSubscriptionResponse.Content.ReadFromJsonAsync<CurrentSubscriptionResponse>(_jsonOptions);
            Assert.NotNull(payload);
            Assert.Equal("Active", payload.Status);
            Assert.Equal(6, payload.LastDurationMonths);
            Assert.NotNull(payload.LastRedeemedAt);
            Assert.NotNull(payload.ExpiryDate);
            Assert.True(payload.ExpiryDate.Value > DateTime.UtcNow.AddMonths(5));
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
                _jsonOptions);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

            var createdUser = await createResponse.Content.ReadFromJsonAsync<UserDto>(_jsonOptions);
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
                options: _jsonOptions);

            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var authResult = await response.Content.ReadFromJsonAsync<AuthenticationResultDto>(_jsonOptions);
            Assert.NotNull(authResult);
            return authResult;
        }

        private static async Task<string> GetAdminAccessToken(HttpClient client)
        {
            _adminAccessToken ??= await AuthHelper.CompleteStartupAsync(client);
            return _adminAccessToken;
        }

        private static SubscriptionConfiguration CreateUpdatedPricing()
        {
            var defaultConfig = new SubscriptionConfiguration();
            return new SubscriptionConfiguration
            {
                BasePricePerMonth = defaultConfig.BasePricePerMonth + 9.5m,
                OneMonthPrice = defaultConfig.OneMonthPrice + 11,
                ThreeMonthPrice = defaultConfig.ThreeMonthPrice + 33,
                SixMonthPrice = defaultConfig.SixMonthPrice + 66.5m,
                TwelveMonthPrice = defaultConfig.TwelveMonthPrice + 99.75m
            };
        }

        private static void AssertPlanBreakdown(SubscriptionConfiguration config)
        {
            Assert.Collection(
                config.Plans,
                oneMonth =>
                {
                    Assert.Equal(1, oneMonth.Months);
                    Assert.Equal(config.OneMonthPrice, oneMonth.Price);
                },
                threeMonth =>
                {
                    Assert.Equal(3, threeMonth.Months);
                    Assert.Equal(config.ThreeMonthPrice, threeMonth.Price);
                },
                sixMonth =>
                {
                    Assert.Equal(6, sixMonth.Months);
                    Assert.Equal(config.SixMonthPrice, sixMonth.Price);
                },
                twelveMonth =>
                {
                    Assert.Equal(12, twelveMonth.Months);
                    Assert.Equal(config.TwelveMonthPrice, twelveMonth.Price);
                });
        }

        private static string CreateTestPassword()
            => $"StrongPass-{Guid.NewGuid():N}!";

        private sealed class AuthenticationResultDto
        {
            public string AccessToken { get; set; } = string.Empty;

            public UserDto User { get; set; } = new();
        }
    }
}
