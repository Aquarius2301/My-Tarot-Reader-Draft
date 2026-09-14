using Microsoft.EntityFrameworkCore;
using MyTarotReader.Application.Common.Exceptions;
using MyTarotReader.Application.Constants.Errors;
using MyTarotReader.Application.Contracts.Persistence;
using MyTarotReader.Application.Contracts.Services;

namespace MyTarotReader.Infrastructure.Services;

public class HistoryService(IAppDbContext context) : IHistoryService
{
    private readonly IAppDbContext _context = context;

    public async Task<List<GetHistoryResult>> GetHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        var history = await _context
            .TarotReadings.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new GetHistoryResult(r.Id, r.CardCode, r.IsReversed, r.CreatedAt))
            .ToListAsync(cancellationToken);

        return history;
    }

    public async Task DeleteHistoryAsync(
        Guid userId,
        Guid historyId,
        CancellationToken cancellationToken = default
    )
    {
        var record =
            await _context.TarotReadings.FirstOrDefaultAsync(
                r => r.Id == historyId && r.UserId == userId,
                cancellationToken
            ) ?? throw new NotFoundException(HistoryErrorCode.NotFound);

        record.DeletedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
