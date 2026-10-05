using Application.Exceptions;
using Application.Interface;
using Application.Results;
using MediatR;

namespace Application.Features.Users.Queries
{
    /// <summary>The caller downloads a copy of the personal data the platform keeps about them.</summary>
    public class ExportMyDataQuery : IRequest<PersonalDataExport>
    {
    }

    public class ExportMyDataQueryHandler : IRequestHandler<ExportMyDataQuery, PersonalDataExport>
    {
        private readonly IPersonalDataReader _reader;
        private readonly ICurrentUser _currentUser;
        private readonly TimeProvider _timeProvider;

        public ExportMyDataQueryHandler(IPersonalDataReader reader, ICurrentUser currentUser, TimeProvider timeProvider)
        {
            _reader = reader;
            _currentUser = currentUser;
            _timeProvider = timeProvider;
        }

        public async Task<PersonalDataExport> Handle(ExportMyDataQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var export = await _reader.ReadAsync(_currentUser.Id.Value, cancellationToken)
                         ?? throw new NotFoundException("User not found.");

            export.ExportedAt = _timeProvider.GetUtcNow().UtcDateTime;
            return export;
        }
    }
}
