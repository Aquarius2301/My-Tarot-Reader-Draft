using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using MyTarotReader.Application.Common.Exceptions;
using MyTarotReader.Application.Constants.Errors;
using MyTarotReader.Application.Contracts.Services;
using MyTarotReader.Domain.Entities;
using MyTarotReader.Domain.Enums;
using MyTarotReader.Infrastructure.Persistence;
using MyTarotReader.Infrastructure.Services;
using StackExchange.Redis;
using Xunit;

namespace MyTarotReader.UnitTest;

/// <summary>
/// Unit tests for <see cref="TarotReadingService"/>. Authenticated-user paths run
/// against a real <see cref="AppDbContext"/> with the EF InMemory provider; guest
/// paths mock the Redis <see cref="IConnectionMultiplexer"/> / <see cref="IDatabase"/>
/// since guest draws live in Redis, not the database.
/// </summary>
public class TarotReadingServiceTests
{
    private const string ValidCard = "maj-00";
    private const string InvalidCard = "fake-card";

    #region Helpers

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .Options;
        return new AppDbContext(options);
    }

    // Guest-path SUT: in-memory DB is unused, Redis is mocked.
    private static (TarotReadingService Service, Mock<IDatabase> Db) CreateSut()
    {
        var db = CreateInMemoryContext();
        var redis = new Mock<IConnectionMultiplexer>();
        var redisDb = new Mock<IDatabase>();
        redis
            .Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(redisDb.Object);
        var service = new TarotReadingService(db, redis.Object);
        return (service, redisDb);
    }

    // Auth-path SUT: real DB + Redis mock (Redis never called in auth flows).
    private static (
        TarotReadingService Service,
        AppDbContext Db,
        Mock<IConnectionMultiplexer> Redis
    ) CreateAuthSut()
    {
        var db = CreateInMemoryContext();
        var redis = new Mock<IConnectionMultiplexer>();
        var service = new TarotReadingService(db, redis.Object);
        return (service, db, redis);
    }

    private static async Task SeedUserAsync(AppDbContext db, Guid userId)
    {
        var user = new User
        {
            Id = userId,
            FullName = $"User-{userId:N}",
            Email = $"user-{userId:N}@example.com",
            Picture = "http://pic",
            ProviderKey = $"provider-{userId:N}",
            Role = UserRole.Registered,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    #endregion

    #region CreateDrawForGuestAsync

    /// <summary>
    /// A valid guest key with a valid card stores the draw in Redis under
    /// <c>tarot:draw:{guestKey}</c> with a 12-hour TTL (When.NotExists), not in the DB.
    /// </summary>
    [Fact]
    public async Task CreateDrawForGuest_ValidKey_CreatesInRedis()
    {
        var (service, dbMock) = CreateSut();
        dbMock
            .Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);
        var request = new CreateDrawForGuestRequest("guest-1", ValidCard, true);

        await service.CreateDrawForGuestAsync(request);

        dbMock.Verify(
            d =>
                d.StringSetAsync(
                    (RedisKey)"tarot:draw:guest-1",
                    It.IsAny<RedisValue>(),
                    It.Is<TimeSpan?>(t => t == TimeSpan.FromHours(12)),
                    When.NotExists,
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
        dbMock.Verify(
            d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Once
        );
    }

    /// <summary>
    /// When a draw already exists in Redis (StringSetAsync returns false), the guest is
    /// rate-limited with TooManyRequestsException using the "drawnAlready" code.
    /// </summary>
    [Fact]
    public async Task CreateDrawForGuest_AlreadyDrawn_ThrowsTooMany()
    {
        var (service, dbMock) = CreateSut();
        dbMock
            .Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(false);

        var request = new CreateDrawForGuestRequest("guest-1", ValidCard, false);

        var act = async () => await service.CreateDrawForGuestAsync(request);

        await act.Should()
            .ThrowAsync<TooManyRequestsException>()
            .Where(e => e.ErrorCode == TarotReadingErrorCode.DrawnAlready);
    }

    /// <summary>
    /// A guest whose previous draw has expired out of the 12-hour TTL can draw again —
    /// the expired key no longer exists, so StringSetAsync succeeds once more.
    /// </summary>
    [Fact]
    public async Task CreateDrawForGuest_AfterCooldown_AllowsAgain()
    {
        var (service, dbMock) = CreateSut();
        // First draw succeeds...
        dbMock
            .Setup(d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    When.NotExists,
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(true);

        await service.CreateDrawForGuestAsync(
            new CreateDrawForGuestRequest("guest-1", ValidCard, false)
        );

        // ...the expired key is gone, so a second draw is allowed too.
        var act = async () =>
            await service.CreateDrawForGuestAsync(
                new CreateDrawForGuestRequest("guest-1", ValidCard, true)
            );

        await act.Should().NotThrowAsync();
        dbMock.Verify(
            d =>
                d.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<When>(),
                    It.IsAny<CommandFlags>()
                ),
            Times.Exactly(2)
        );
    }

    #endregion

    #region GetLastDrawnCardForGuestAsync

    /// <summary>
    /// A guest with a draw still within the cooldown window gets back the stored
    /// CardCode/IsReversed and a positive RemainingSeconds matching the TTL left.
    /// </summary>
    [Fact]
    public async Task GetLastDrawnCardForGuest_HasDraw_ReturnsCardAndRemaining()
    {
        var (service, dbMock) = CreateSut();
        var remaining = TimeSpan.FromHours(10);
        var payload = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                DrawnAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CardCode = ValidCard,
                IsReversed = true,
            }
        );
        dbMock
            .Setup(d =>
                d.StringGetWithExpiryAsync((RedisKey)"tarot:draw:guest-1", It.IsAny<CommandFlags>())
            )
            .ReturnsAsync(new RedisValueWithExpiry((RedisValue)payload, remaining));

        var result = await service.GetLastDrawnCardForGuestAsync("guest-1");

        result.CardCode.Should().Be(ValidCard);
        result.IsReversed.Should().BeTrue();
        result.RemainingSeconds.Should().Be((long)remaining.TotalSeconds);
    }

    /// <summary>
    /// A guest with no draw (or an expired one) gets an empty result, not null.
    /// </summary>
    [Fact]
    public async Task GetLastDrawnCardForGuest_NoDraw_ReturnsEmpty()
    {
        var (service, dbMock) = CreateSut();
        dbMock
            .Setup(d => d.StringGetWithExpiryAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValueWithExpiry(RedisValue.Null, null));

        var result = await service.GetLastDrawnCardForGuestAsync("guest-1");

        result.CardCode.Should().BeEmpty();
        result.IsReversed.Should().BeFalse();
        result.RemainingSeconds.Should().Be(0);
    }

    #endregion

    #region CreateDrawForAuthAsync

    /// <summary>
    /// A valid draw for an authenticated user is stored in the DB, tied to the correct
    /// userId with the requested card/reversed flags.
    /// </summary>
    [Fact]
    public async Task CreateDrawForAuth_Valid_StoresForUser()
    {
        var (service, db, _) = CreateAuthSut();
        var userId = Guid.NewGuid();

        await service.CreateDrawForAuthAsync(new CreateDrawForAuthRequest(ValidCard, true), userId);

        var entity = Assert.Single(db.TarotReadings);
        entity.UserId.Should().Be(userId);
        entity.CardCode.Should().Be(ValidCard);
        entity.IsReversed.Should().BeTrue();
    }

    #endregion

    #region GetLastDrawnCardForAuthAsync

    /// <summary>
    /// A user with draws gets the most recent one (newest CreatedAt), correctly mapped.
    /// </summary>
    [Fact]
    public async Task GetLastDrawnCardForAuth_HasDraw_ReturnsLatest()
    {
        var (service, db, _) = CreateAuthSut();
        var userId = Guid.NewGuid();
        await SeedUserAsync(db, userId);
        db.TarotReadings.Add(
            new TarotReading
            {
                UserId = userId,
                CardCode = "maj-01",
                IsReversed = false,
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            }
        );
        db.TarotReadings.Add(
            new TarotReading
            {
                UserId = userId,
                CardCode = ValidCard,
                IsReversed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var result = await service.GetLastDrawnCardForAuthAsync(userId);

        result.Should().NotBeNull();
        result!.CardCode.Should().Be(ValidCard);
        result.IsReversed.Should().BeTrue();
    }

    /// <summary>
    /// A user with no draws gets null.
    /// </summary>
    [Fact]
    public async Task GetLastDrawnCardForAuth_NoDraw_ReturnsNull()
    {
        var (service, _, _) = CreateAuthSut();

        var result = await service.GetLastDrawnCardForAuthAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    /// <summary>
    /// Only the requested user's draws are considered; another user's new draw is ignored.
    /// </summary>
    [Fact]
    public async Task GetLastDrawnCardForAuth_OnlyOwnUserDraws()
    {
        var (service, db, _) = CreateAuthSut();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedUserAsync(db, userA);
        await SeedUserAsync(db, userB);
        db.TarotReadings.Add(
            new TarotReading
            {
                UserId = userA,
                CardCode = ValidCard,
                IsReversed = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            }
        );
        db.TarotReadings.Add(
            new TarotReading
            {
                UserId = userB,
                CardCode = "maj-21",
                IsReversed = true,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await db.SaveChangesAsync();

        var result = await service.GetLastDrawnCardForAuthAsync(userA);

        result!.CardCode.Should().Be(ValidCard);
        result.IsReversed.Should().BeFalse();
    }

    #endregion

    #region RemoveDrawForGuestAsync

    /// <summary>
    /// Removing an existing guest draw deletes the Redis key.
    /// </summary>
    [Fact]
    public async Task RemoveDrawForGuest_ValidKey_Deletes()
    {
        var (service, dbMock) = CreateSut();
        dbMock
            .Setup(d => d.KeyDeleteAsync((RedisKey)"tarot:draw:guest-1", It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        await service.RemoveDrawForGuestAsync("guest-1");

        dbMock.Verify(
            d => d.KeyDeleteAsync((RedisKey)"tarot:draw:guest-1", It.IsAny<CommandFlags>()),
            Times.Once
        );
    }

    /// <summary>
    /// Removing a key that does not exist is a no-op (idempotent), no throw.
    /// </summary>
    [Fact]
    public async Task RemoveDrawForGuest_NoDraw_NoThrow()
    {
        var (service, dbMock) = CreateSut();
        dbMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(false);

        var act = async () => await service.RemoveDrawForGuestAsync("guest-1");

        await act.Should().NotThrowAsync();
    }

    #endregion
}
