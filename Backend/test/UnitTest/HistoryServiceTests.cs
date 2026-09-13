using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MyTarotReader.Application.Common.Exceptions;
using MyTarotReader.Application.Constants.Errors;
using MyTarotReader.Domain.Entities;
using MyTarotReader.Domain.Enums;
using MyTarotReader.Infrastructure.Persistence;
using MyTarotReader.Infrastructure.Services;
using Xunit;

namespace MyTarotReader.UnitTest;

/// <summary>
/// Unit tests for <see cref="HistoryService"/>, running against a real
/// <see cref="AppDbContext"/> with the EF Core InMemory provider so that the
/// global query filter (<c>DeletedAt == null</c>) and the <c>UserId</c> scoping
/// behave like production.
/// </summary>
public class HistoryServiceTests
{
    #region Helpers

    private static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableServiceProviderCaching(false)
            .Options;
        return new AppDbContext(options);
    }

    private static HistoryService CreateSut(AppDbContext db) => new(db);

    private static async Task<(TarotReading Reading, User User)> SeedReadingAsync(
        AppDbContext db,
        Guid userId,
        string cardCode,
        bool isReversed,
        DateTimeOffset? createdAt = null
    )
    {
        // A User row is only added once per user id; subsequent readings for the same
        // user reuse the already-tracked User instead of adding a conflicting instance.
        var user = db.Users.Local.FirstOrDefault(u => u.Id == userId);
        if (user == null)
        {
            user = new User
            {
                Id = userId,
                FullName = $"User-{userId:N}",
                Email = $"user-{userId:N}@example.com",
                Picture = "http://pic",
                ProviderKey = $"provider-{userId:N}",
                Role = UserRole.Registered,
            };
            db.Users.Add(user);
        }

        var reading = new TarotReading
        {
            UserId = userId,
            CardCode = cardCode,
            IsReversed = isReversed,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };
        db.TarotReadings.Add(reading);
        await db.SaveChangesAsync();
        return (reading, user);
    }

    #endregion

    #region GetHistoryAsync

    /// <summary>
    /// A user with readings gets back the full list with every field mapped from the
    /// stored rows (Id, CardCode, IsReversed, CreatedAt).
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_UserHasHistory_ReturnsMappedHistory()
    {
        var db = CreateInMemoryContext();
        var userA = Guid.NewGuid();
        var (seed, _) = await SeedReadingAsync(db, userA, "maj-00", false);
        // Second reading for the same user.
        var second = new TarotReading
        {
            UserId = userA,
            CardCode = "min-wands-1",
            IsReversed = true,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        db.TarotReadings.Add(second);
        await db.SaveChangesAsync();

        var service = CreateSut(db);
        var result = await service.GetHistoryAsync(userA);

        result.Should().HaveCount(2);
        var first = result.Single(x => x.CardCode == "maj-00");
        first.Id.Should().Be(seed.Id);
        first.CardCode.Should().Be("maj-00");
        first.IsReversed.Should().BeFalse();
        first.CreatedAt.Should().Be(seed.CreatedAt);

        var secondResult = result.Single(x => x.CardCode == "min-wands-1");
        secondResult.Id.Should().Be(second.Id);
        secondResult.IsReversed.Should().BeTrue();
        secondResult.CreatedAt.Should().Be(second.CreatedAt);
    }

    /// <summary>
    /// A user with no readings gets back an empty (non-null) list.
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_NoHistory_ReturnsEmptyList()
    {
        var db = CreateInMemoryContext();
        var service = CreateSut(db);

        var result = await service.GetHistoryAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Only readings belonging to the requested user are returned; another user's
    /// readings are never mixed in.
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_OnlyReturnsUsersOwnHistory()
    {
        var db = CreateInMemoryContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await SeedReadingAsync(db, userA, "maj-00", false);
        await SeedReadingAsync(db, userA, "min-wands-1", true);
        await SeedReadingAsync(db, userB, "maj-10", false);

        var service = CreateSut(db);
        var result = await service.GetHistoryAsync(userA);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r => r.CardCode.Should().BeOneOf("maj-00", "min-wands-1"));
    }

    /// <summary>
    /// Readings are returned newest-first (ordered by CreatedAt descending).
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_ReturnsOrderedByCreatedAtDescending()
    {
        var db = CreateInMemoryContext();
        var user = Guid.NewGuid();
        var oldest = DateTimeOffset.UtcNow.AddDays(-2);
        var middle = DateTimeOffset.UtcNow.AddDays(-1);
        var newest = DateTimeOffset.UtcNow;
        await SeedReadingAsync(db, user, "oldest", false, oldest);
        await SeedReadingAsync(db, user, "middle", true, middle);
        await SeedReadingAsync(db, user, "newest", false, newest);

        var service = CreateSut(db);
        var result = await service.GetHistoryAsync(user);

        result.Select(x => x.CreatedAt).Should().Equal(newest, middle, oldest);
    }

    /// <summary>
    /// Soft-deleted readings (DeletedAt set) are excluded by the global query filter.
    /// </summary>
    [Fact]
    public async Task GetHistoryAsync_ExcludesSoftDeletedRecords()
    {
        var db = CreateInMemoryContext();
        var user = Guid.NewGuid();
        await SeedReadingAsync(db, user, "kept", false);
        var (deleted, _) = await SeedReadingAsync(db, user, "deleted", true);
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var service = CreateSut(db);
        var result = await service.GetHistoryAsync(user);

        result.Should().HaveCount(1);
        result.Single().CardCode.Should().Be("kept");
    }

    #endregion

    #region DeleteHistoryAsync

    /// <summary>
    /// Deleting an existing reading soft-deletes it (DeletedAt set) and makes it
    /// disappear from further GetHistoryAsync results.
    /// </summary>
    [Fact]
    public async Task DeleteHistoryAsync_ExistingRecord_SoftDeletes()
    {
        var db = CreateInMemoryContext();
        var user = Guid.NewGuid();
        var (reading, _) = await SeedReadingAsync(db, user, "maj-00", false);

        var service = CreateSut(db);
        await service.DeleteHistoryAsync(user, reading.Id);

        db.TarotReadings.IgnoreQueryFilters()
            .Single(r => r.Id == reading.Id)
            .DeletedAt.Should()
            .NotBeNull();
        var afterDelete = await service.GetHistoryAsync(user);
        afterDelete.Should().BeEmpty();
    }

    /// <summary>
    /// A history id that does not exist throws NotFoundException with the history
    /// error code.
    /// </summary>
    [Fact]
    public async Task DeleteHistoryAsync_HistoryIdNotExist_ThrowsNotFound()
    {
        var db = CreateInMemoryContext();
        var user = Guid.NewGuid();
        var service = CreateSut(db);

        var act = async () => await service.DeleteHistoryAsync(user, Guid.NewGuid());

        await act.Should()
            .ThrowAsync<NotFoundException>()
            .Where(e => e.ErrorCode == HistoryErrorCode.NotFound);
    }

    /// <summary>
    /// A reading that exists but belongs to a different user is treated as not found
    /// (NotFoundException) and the other user's reading is left untouched.
    /// </summary>
    [Fact]
    public async Task DeleteHistoryAsync_RecordBelongsToOtherUser_ThrowsNotFound()
    {
        var db = CreateInMemoryContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var (readingB, _) = await SeedReadingAsync(db, userB, "maj-00", false);

        var service = CreateSut(db);
        var act = async () => await service.DeleteHistoryAsync(userA, readingB.Id);

        await act.Should()
            .ThrowAsync<NotFoundException>()
            .Where(e => e.ErrorCode == HistoryErrorCode.NotFound);
        db.TarotReadings.IgnoreQueryFilters()
            .Single(r => r.Id == readingB.Id)
            .DeletedAt.Should()
            .BeNull();
    }

    #endregion
}
