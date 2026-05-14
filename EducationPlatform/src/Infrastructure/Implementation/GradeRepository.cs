using Infrastructure.Interface;
using Infrastructure.Persistence;
using Domain.AcademicManagement.Aggregate;

namespace Infrastructure.Implementation
{
    public class GradeRepository :
        GenericRepository<Grade>,
        IGradeRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public GradeRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        #endregion
    }
}

