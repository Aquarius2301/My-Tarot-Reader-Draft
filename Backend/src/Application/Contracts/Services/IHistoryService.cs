namespace MyTarotReader.Application.Contracts.Services;

/// <summary>
/// Represents the result of retrieving a user's tarot reading history.
/// </summary>
public record GetHistoryResult(Guid Id, string CardCode, bool IsReversed, DateTimeOffset CreatedAt);

public interface IHistoryService
{
    /// <summary>
    /// Retrieves the history of tarot readings for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user for whom to retrieve the history.</param>
    /// <returns>A list of <see cref="GetHistoryResult"/> representing the user's tarot reading history.</returns>
    Task<List<GetHistoryResult>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Deletes a specific history entry for a user.
    /// </summary>
    /// <param name="userId">The ID of the user for whom to delete the history entry.</param>
    /// <param name="historyId">The ID of the history entry to delete.</param>
    /// <exception cref="NotFoundException">Thrown when the specified history entry does not exist for the user.</exception>
    Task DeleteHistoryAsync(
        Guid userId,
        Guid historyId,
        CancellationToken cancellationToken = default
    );
}
