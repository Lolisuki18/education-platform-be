using Application.Interface;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.Common.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;

namespace Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICourseRepository _courseRepository;
        private readonly IStorageService _storageService;
        private readonly ICurrentUser _currentUser;

        public CreateCourseCommandHandler(
            IUnitOfWork unitOfWork,
            ICourseRepository courseRepository,
            IStorageService storageService,
            ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _courseRepository = courseRepository;
            _storageService = storageService;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            string thumbnailName = request.ThumbnailName;
            if (request.ThumbnailFileStream != null && !string.IsNullOrEmpty(request.ThumbnailFileExtension))
            {
                var length = request.ThumbnailFileStream.CanSeek ? request.ThumbnailFileStream.Length : 0;
                var ext = "." + request.ThumbnailFileExtension.TrimStart('.').ToLowerInvariant();

                // Thumbnails are served publicly: anything but a picture would turn the platform into a file host
                if (ext is not (".jpg" or ".jpeg" or ".png"))
                    throw new BadRequestException("Thumbnail must be an image (.jpg, .jpeg, .png).");

                Application.Helpers.FileValidator.Validate(request.ThumbnailFileStream, length, "thumbnail" + ext);

                thumbnailName = await _storageService.SaveAsync(
                    request.ThumbnailFileStream,
                    request.ThumbnailFileExtension.TrimStart('.'),
                    cancellationToken);
            }

            // Slugs identify a course in public URLs, so they must be unique: a taken one gets a short suffix
            var slug = Domain.Common.Slugs.Create(request.Slug, request.Title);
            if (await _courseRepository.SlugExistsAsync(slug, cancellationToken))
            {
                slug = Domain.Common.Slugs.WithSuffix(slug);
            }

            // Apply domain logic: create the Course aggregate
            var course = new Domain.CourseManagement.Aggregate.Course(
                Guid.NewGuid(),
                request.Title,
                request.Description,
                request.Price,
                thumbnailName,
                slug,
                request.Prerequisites,
                request.LearningOutcomes,
                _currentUser.Id.Value,
                request.GradeID,
                request.SubjectID,
                DateTime.UtcNow);

            foreach (var chapterDto in request.Chapters)
            {
                var chapter = course.AddChapter(
                    chapterDto.Title,
                    chapterDto.Description,
                    chapterDto.Order);

                foreach (var lessonDto in chapterDto.Lessons)
                {
                    var lesson = chapter.AddLesson(
                        lessonDto.Title,
                        lessonDto.Objectives,
                        lessonDto.Description,
                        lessonDto.VideoUrl);

                    foreach (var quizDto in lessonDto.Quizzes)
                    {
                        var quiz = lesson.AddQuiz(
                            quizDto.Question,
                            quizDto.Note);

                        quiz.AddAnswer(
                            (QuizType)quizDto.Answer.Type,
                            quizDto.Answer.CorrectAnswers,
                            quizDto.Answer.Options,
                            quizDto.Answer.TrueOrFalse);
                    }

                    foreach (var materialDto in lessonDto.Materials)
                    {
                        var material = lesson.AddMaterial(
                            materialDto.Name,
                            materialDto.Description,
                            materialDto.Url,
                            materialDto.Type);
                    }

                    foreach (var assignmentDto in lessonDto.Assignments)
                    {
                        var assignment = lesson.AddAssignment(
                            assignmentDto.Title,
                            assignmentDto.Description,
                            assignmentDto.MaxScore);
                    }
                }
            }

            // Apply persistence
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                _courseRepository.Add(course);

                await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
            }
            catch
            {
                if (request.ThumbnailFileStream != null && thumbnailName != request.ThumbnailName)
                {
                    await _storageService.DeleteAsync(thumbnailName);
                }
                throw;
            }

            return course.CourseID;
        }
    }
}
