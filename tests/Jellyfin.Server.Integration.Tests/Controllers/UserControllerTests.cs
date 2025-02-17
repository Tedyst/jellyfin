using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Api.Models.UserDtos;
using Jellyfin.Extensions.Json;
using MediaBrowser.Model.Dto;
using Xunit;
using Xunit.Priority;

namespace Jellyfin.Server.Integration.Tests.Controllers
{
    [TestCaseOrderer(PriorityOrderer.Name, PriorityOrderer.Assembly)]
    public sealed class UserControllerTests : IClassFixture<JellyfinApplicationFactory>
    {
        private const string TestUsername = "testUser01";

        private readonly JellyfinApplicationFactory _factory;
        private readonly JsonSerializerOptions _jsonOpions = JsonDefaults.Options;
        private static string? _accessToken;
        private static Guid _testUserId = Guid.Empty;

        public UserControllerTests(JellyfinApplicationFactory factory)
        {
            _factory = factory;
        }

        private Task<HttpResponseMessage> CreateUserByName(HttpClient httpClient, CreateUserByName request)
            => httpClient.PostAsJsonAsync("Users/New", request, _jsonOpions);

        private Task<HttpResponseMessage> UpdateUserPassword(HttpClient httpClient, Guid userId, UpdateUserPassword request)
            => httpClient.PostAsJsonAsync("Users/" + userId.ToString("N", CultureInfo.InvariantCulture) + "/Password", request, _jsonOpions);

        [Fact]
        [Priority(-1)]
        public async Task GetPublicUsers_Valid_Success()
        {
            var client = _factory.CreateClient();

            using var response = await client.GetAsync("Users/Public").ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var users = await JsonSerializer.DeserializeAsync<UserDto[]>(
                await response.Content.ReadAsStreamAsync().ConfigureAwait(false), _jsonOpions).ConfigureAwait(false);
            // User are hidden by default
            Assert.Empty(users);
        }

        [Fact]
        [Priority(-1)]
        public async Task GetUsers_Valid_Success()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client).ConfigureAwait(false));

            using var response = await client.GetAsync("Users").ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var users = await JsonSerializer.DeserializeAsync<UserDto[]>(
                await response.Content.ReadAsStreamAsync().ConfigureAwait(false), _jsonOpions).ConfigureAwait(false);
            Assert.Single(users);
            Assert.False(users![0].HasConfiguredPassword);
        }

        [Fact]
        [Priority(0)]
        public async Task New_Valid_Success()
        {
            var client = _factory.CreateClient();

            // access token can't be null here as the previous test populated it
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken!);

            var createRequest = new CreateUserByName()
            {
                Name = TestUsername
            };

            using var response = await CreateUserByName(client, createRequest).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var user = await JsonSerializer.DeserializeAsync<UserDto>(
                await response.Content.ReadAsStreamAsync().ConfigureAwait(false), _jsonOpions).ConfigureAwait(false);
            Assert.Equal(TestUsername, user!.Name);
            Assert.False(user.HasPassword);
            Assert.False(user.HasConfiguredPassword);

            _testUserId = user.Id;

            Console.WriteLine(user.Id.ToString("N", CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("‼️")]
        [Priority(0)]
        public async Task New_Invalid_Fail(string? username)
        {
            var client = _factory.CreateClient();

            // access token can't be null here as the previous test populated it
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken!);

            var createRequest = new CreateUserByName()
            {
                Name = username
            };

            using var response = await CreateUserByName(client, createRequest).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Priority(1)]
        public async Task UpdateUserPassword_Valid_Success()
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.AddAuthHeader(_accessToken!);

            var createRequest = new UpdateUserPassword()
            {
                NewPw = "4randomPa$$word"
            };

            using var response = await UpdateUserPassword(client, _testUserId, createRequest).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var users = await JsonSerializer.DeserializeAsync<UserDto[]>(
                await client.GetStreamAsync("Users").ConfigureAwait(false), _jsonOpions).ConfigureAwait(false);
            var user = users!.First(x => x.Id == _testUserId);
            Assert.True(user.HasPassword);
            Assert.True(user.HasConfiguredPassword);
        }

        [Fact]
        [Priority(2)]
        public async Task UpdateUserPassword_Empty_RemoveSetPassword()
        {
            var client = _factory.CreateClient();

            client.DefaultRequestHeaders.AddAuthHeader(_accessToken!);

            var createRequest = new UpdateUserPassword()
            {
                CurrentPw = "4randomPa$$word",
            };

            using var response = await UpdateUserPassword(client, _testUserId, createRequest).ConfigureAwait(false);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var users = await JsonSerializer.DeserializeAsync<UserDto[]>(
                await client.GetStreamAsync("Users").ConfigureAwait(false), _jsonOpions).ConfigureAwait(false);
            var user = users!.First(x => x.Id == _testUserId);
            Assert.False(user.HasPassword);
            Assert.False(user.HasConfiguredPassword);
        }
    }
}
