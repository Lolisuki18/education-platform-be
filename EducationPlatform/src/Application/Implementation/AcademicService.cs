using AutoMapper;
using Application.BusinessException;
using Application.Results;
using Application.Interface;
using Infrastructure.Interface;

namespace Application.Implementation
{
    public class AcademicService : IAcademicService
    {
        #region Attributes
        private readonly IMapper mapper;
        private readonly IUnitOfWork unitOfWork;
        #endregion

        #region Properties
        #endregion

        public AcademicService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            this.mapper = mapper;
            this.unitOfWork = unitOfWork;
        }

        #region Methods
        public async Task<IEnumerable<SubjectDTO>> GetSubjects()
        {
            // Validate subject list existence
            var list = await unitOfWork
                .GetRepository<ISubjectRepository>()
                .GetAllAsync();

            if (list == null || !list.Any())
                throw new NotFound(
                    "Subject list is empty or was not found");

            return mapper.Map<IEnumerable<SubjectDTO>>(list);
        }

        public async Task<IEnumerable<GradeDTO>> GetGrades()
        {
            // Validate subject list existence
            var list = await unitOfWork
                .GetRepository<IGradeRepository>()
                .GetAllAsync();

            if (list == null || !list.Any())
                throw new NotFound(
                    "Grade list is empty or was not found");

            return mapper.Map<IEnumerable<GradeDTO>>(list);
        }

        public async Task<IEnumerable<DefaultLessonDTO>> GetDefaultLessons(
            Guid subjectId, 
            Guid gradeId)
        {
            // Validate default lessons list existence
            var list = await unitOfWork
                .GetRepository<ISubjectRepository>()
                .GetDefaultLessons(subjectId, gradeId);

            if (list == null || !list.Any())
                throw new NotFound(
                    "Default lessons list is empty or was not found");

            return mapper.Map<IEnumerable<DefaultLessonDTO>>(list);
        }
        #endregion
    }
}


