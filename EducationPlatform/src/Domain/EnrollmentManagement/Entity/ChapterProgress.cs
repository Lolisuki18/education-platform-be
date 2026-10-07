using Domain.CourseManagement.Entity;
using Domain.Exceptions;

namespace Domain.EnrollmentManagement.Entity
{
    public class ChapterProgress
    {
        #region Attributes
        private readonly List<LessonProgress> lessonProgresses = new();
        #endregion

        #region Properties
        public Guid ChapterProgressID { get; private set; }
        public bool IsCompleted { get; private set; }

        public Guid CourseProgressID { get; private set; }
        public Guid ChapterID { get; private set; }

        public IReadOnlyCollection<LessonProgress> LessonProgresses
        {
            get { return lessonProgresses.AsReadOnly(); }
        }

        public Chapter Chapter { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected ChapterProgress() { }
#pragma warning restore CS8618

        public ChapterProgress(Guid chapterProgressId, Guid courseProgressId, Guid chapterId)
        {
            if (chapterProgressId == Guid.Empty)
                throw new DomainException("Chapter progress ID cannot be empty");
            if (courseProgressId == Guid.Empty)
                throw new DomainException("Course progress ID cannot be empty");
            if (chapterId == Guid.Empty)
                throw new DomainException("Chapter ID cannot be empty");

            ChapterProgressID = chapterProgressId;
            CourseProgressID = courseProgressId;
            ChapterID = chapterId;
        }

        public LessonProgress AddLessonProgress(Guid lessonId)
        {
            var lessonProgress = new LessonProgress(Guid.NewGuid(), ChapterProgressID, lessonId);
            lessonProgresses.Add(lessonProgress);
            return lessonProgress;
        }

        /// <summary>The chapter is finished when every lesson the course has in it is finished.</summary>
        public void RecalculateCompletion(IReadOnlyCollection<LessonOutline> outline)
        {
            var chapterLessons = outline.Where(o => o.ChapterID == ChapterID).ToList();

            foreach (var lesson in lessonProgresses)
            {
                lesson.RecalculateCompletion(chapterLessons.FirstOrDefault(o => o.LessonID == lesson.LessonID)?.QuizCount ?? 0);
            }

            IsCompleted = chapterLessons.Count > 0 &&
                          chapterLessons.All(o => lessonProgresses.Any(l => l.LessonID == o.LessonID && l.IsCompleted));
        }
    }

}

