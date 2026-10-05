
using Application.Exceptions;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using MediatR;

namespace Application.Features.StudentReview.Command
{
    public class CreateReviewCommand : IRequest<CourseReviewDTO>
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public Guid CourseId { get; set; }

        public float Rating { get; set; }

        public string? Comment { get; set; }

    }

    public class CreateReviewCommandHandler : IRequestHandler<CreateReviewCommand, CourseReviewDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public CreateReviewCommandHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<CourseReviewDTO> Handle(CreateReviewCommand request, CancellationToken cancellationToken)
        {
            //1. Check if the user is authenticated and has the role of Student
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated to create a review.");
            if (_currentUser.Role != Role.Student.ToString())
                throw new AuthenticateException("Only students can create reviews in course");

            var currentStudentId = _currentUser.Id.Value;
            //2. Check if the student has enrolled in the course
            var enrollments = await _unitOfWork.GetRepository<IEnrollmentRepository>().GetStudentEnrollments(currentStudentId);
            if (!enrollments.Any(e => e.CourseID == request.CourseId))
            {
                throw new ForbiddenException("You must be enrolled in this course to leave a review.");
            }

            // Check if the student has already reviewed this course
            var hasReviewed = await _unitOfWork
                .GetRepository<ICourseReviewRepository>()
                .HasStudentReviewedCourseAsync(request.CourseId, currentStudentId);
            if (hasReviewed)
            {
                throw new ConflictException("You have already reviewed this course.");
            }

            //3. Find the course by courseId
            var course = await _unitOfWork.GetRepository<ICourseRepository>().GetByIdAsync(request.CourseId);
            if (course == null) throw new NotFoundException("Course not found.");

            //4. Find the student by studentId
            var student = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(currentStudentId);
            if (student == null) throw new NotFoundException("Student not found.");

            //5. Create a new review and add it to the CourseReview 

            var review = new CourseReview(request.CourseId, currentStudentId, request.Rating, request.Comment);

            //6. Add to the repository and save changes
            await _unitOfWork.GetRepository<ICourseReviewRepository>().AddAsync(review);

            //save in database
            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            //7. Map the created review to CourseReviewDTO and return it
            var result = _mapper.Map<CourseReviewDTO>(review);
            result.CourseName = course.Title;
            result.Reviewer = student.Name;
            return result;

        }
    }
}