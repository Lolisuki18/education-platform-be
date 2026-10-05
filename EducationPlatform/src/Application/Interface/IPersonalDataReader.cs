using Application.Results;

namespace Application.Interface
{
    /// <summary>Collects, in one place, every record that belongs to a user (spread over many aggregates).</summary>
    public interface IPersonalDataReader
    {
        Task<PersonalDataExport?> ReadAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
