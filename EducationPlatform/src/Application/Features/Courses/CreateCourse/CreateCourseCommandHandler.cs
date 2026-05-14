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

namespace Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandHandler : IRequestHandler<CreateCourseCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICourseRepository _courseRepository;
        private readonly IStorageService _storageService;

        public CreateCourseCommandHandler(IUnitOfWork unitOfWork, ICourseRepository courseRepository, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _courseRepository = courseRepository;
            _storageService = storageService;
        }

        public async Task<Guid> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
        {
            string thumbnailName = request.ThumbnailName;
            if (request.ThumbnailFile != null)
            {
                thumbnailName = await _storageService.SaveAsync(
                    request.ThumbnailFile.OpenReadStream(),
                    Path.GetExtension(request.ThumbnailFile.FileName).TrimStart('.'),
                    cancellationToken);
            }

            // Apply domain logic: create the Course aggregate
            var course = new Domain.CourseManagement.Aggregate.Course(
                Guid.NewGuid(),
                request.Title,
                request.Description,
                request.Price,
                thumbnailName,
                request.Slug,
                request.Prerequisites,
                request.LearningOutcomes,
                request.CallerId,
                request.GradeID,
                request.SubjectID,
                DateTime.UtcNow);

            var chapters = new List<Chapter>();
            var lessons = new List<Lesson>();
            var quizzes = new List<Quiz>();
            var assignments = new List<Assignment>();
            var materials = new List<Material>();

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

                        quizzes.Add(quiz);
                    }

                    foreach (var materialDto in lessonDto.Materials)
                    {
                        var material = lesson.AddMaterial(
                            materialDto.Name,
                            materialDto.Description,
                            materialDto.Url,
                            materialDto.Type);

                        materials.Add(material);
                    }

                    foreach (var assignmentDto in lessonDto.Assignments)
                    {
                        var assignment = lesson.AddAssignment(
                            assignmentDto.Title,
                            assignmentDto.Description,
                            assignmentDto.MaxScore);

                        assignments.Add(assignment);
                    }

                    lessons.Add(lesson);
                }

                chapters.Add(chapter);
            }

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            
            _courseRepository.Add(course);
            _courseRepository.AddChapters(chapters);
            _courseRepository.AddLessons(lessons);
            _courseRepository.AddQuizzes(quizzes);
            _courseRepository.AddAssignments(assignments);
            _courseRepository.AddMaterials(materials);
            
            await _unitOfWork.CommitAsync(request.CallerId.ToString());

            return course.CourseID;
        }
    }
}
