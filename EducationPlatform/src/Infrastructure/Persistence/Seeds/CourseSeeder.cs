using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class CourseSeeder
    {
        public static async Task<CourseSeedResult> SeedAsync(
            EducationPlatformDBContext context,
            List<Guid> teacherIds,
            Dictionary<int, Guid> gradeIds,
            Dictionary<string, Guid> subjectIds)
        {
            var result = new CourseSeedResult();

            if (await context.Set<Course>().AnyAsync())
                return result;

            var random = new Random();

            var courses = new List<Course>();

            int courseCount = 900;

            var subjectList = subjectIds.ToList();
            var gradeList = gradeIds.ToList();
            var thumbnailFiles = new[]
            {
                "default-thumbnail-1.jpg",
                "default-thumbnail-2.jpg"
            };

            for (int i = 1; i <= courseCount; i++)
            {
                var isRejected = random.Next(0, 14) == 0;
                
                DateTime reviewDate = DateTime.Now.AddDays(-random.Next(0, 10));

                var teacherId = teacherIds[random.Next(teacherIds.Count)];

                var gradePair = gradeList[random.Next(gradeList.Count)];
                var subjectPair = subjectList[random.Next(subjectList.Count)];

                int gradeNumber = gradePair.Key;
                Guid gradeId = gradePair.Value;

                string subjectCode = subjectPair.Key;
                Guid subjectId = subjectPair.Value;

                var title = $"{subjectCode} Grade {gradeNumber} - Course {i}";
                var thumbnailPath = Path.Combine(
                    "2026",
                    "03",
                    thumbnailFiles[random.Next(thumbnailFiles.Length)]
                ).Replace("\\", "/");
                var price = (decimal?)random.Next(15000, 100000);

                var course = new Course(
                    Guid.NewGuid(),
                    title,
                    $"This is a course about {subjectCode} for Grade {gradeNumber}",
                    price: price,
                    thumbnailName: thumbnailPath,
                    slug: $"course-{i}",
                    prerequisites: "Basic knowledge required",
                    learningOutcomes: "Understand core concepts",
                    teacherId: teacherId,
                    gradeId: gradeId,
                    subjectId: subjectId,
                    createdAt: RandomDate(random)
                );

                int chapterCount = random.Next(3, 7);

                for (int c = 1; c <= chapterCount; c++)
                {
                    var chapter = course.AddChapter(
                        $"Chapter {c}",
                        $"Description for chapter {c}",
                        c
                    );

                    // -----------------------
                    // Add Lessons
                    // -----------------------
                    int lessonCount = random.Next(3, 6);

                    for (int l = 1; l <= lessonCount; l++)
                    {
                        var lesson = chapter.AddLesson(
                            $"Lesson {l}",
                            $"Objectives for lesson {l}",
                            $"Description for lesson {l}",
                            $"/videos/default.mp4"
                        );

                        // -----------------------
                        // Add Quizzes
                        // -----------------------
                        int quizCount = random.Next(2, 5);

                        for (int q = 1; q <= quizCount; q++)
                        {
                            var quiz = lesson.AddQuiz(
                                $"Question {q} for lesson {l}",
                                "Auto-generated quiz"
                            );

                            var quizType = (QuizType)random.Next(1, 4);

                            switch (quizType)
                            {
                                case QuizType.SingleChoice:
                                    {
                                        var options = new List<string>
                                        {
                                            "Option A",
                                            "Option B",
                                            "Option C",
                                            "Option D"
                                        };

                                        var correct = options[random.Next(options.Count)];

                                        quiz.AddAnswer(
                                            QuizType.SingleChoice,
                                            new List<string> { correct },
                                            options
                                        );

                                        break;
                                    }

                                case QuizType.MultipleChoice:
                                    {
                                        var options = new List<string>
                                        {
                                            "Option A",
                                            "Option B",
                                            "Option C",
                                            "Option D"
                                        };

                                        var correctAnswers = options
                                            .OrderBy(x => random.Next())
                                            .Take(random.Next(2, 4))
                                            .ToList();

                                        quiz.AddAnswer(
                                            QuizType.MultipleChoice,
                                            correctAnswers,
                                            options
                                        );

                                        break;
                                    }

                                case QuizType.TrueFalse:
                                    {
                                        var value = random.Next(0, 2) == 1;

                                        quiz.AddAnswer(
                                            QuizType.TrueFalse,
                                            answers: new List<string> { value.ToString() },
                                            options: new List<string> { "True", "False" },
                                            trueFalseValue: value
                                        );

                                        break;
                                    }
                            }
                        }
                    }
                }

                if (isRejected)
                {
                    course.MarkAsRejected(reviewDate, "Auto rejected during seeding");
                }
                else
                {
                    course.MarkAsPublished(reviewDate);
                }

                courses.Add(course);
                result.CoursePrices[course.CourseID] = price;
                result.CourseIds.Add(course.CourseID);
            }

            context.AddRange(courses);

            return result;
        }

        private static DateTime RandomDate(Random random)
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddYears(-3);

            var range = (endDate - startDate).TotalSeconds;

            return startDate.AddSeconds(random.NextDouble() * range);
        }
    }
}
