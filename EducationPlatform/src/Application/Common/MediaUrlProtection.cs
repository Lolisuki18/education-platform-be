using Application.Interface;
using Application.Results;

namespace Application.Common
{
    /// <summary>Replaces the video links in a DTO by signed, expiring ones. Call it only for callers allowed to watch.</summary>
    public static class MediaUrlProtection
    {
        public static CourseDetailDTO ProtectVideos(this CourseDetailDTO course, IMediaUrlSigner signer)
        {
            ProtectChapters(course.Chapters, signer);
            return course;
        }

        public static EnrollmentDetailDTO ProtectVideos(this EnrollmentDetailDTO enrollment, IMediaUrlSigner signer)
        {
            enrollment.Course?.ProtectVideos(signer);

            foreach (var chapterProgress in enrollment.CourseProgress?.ChapterProgresses ?? new List<ChapterProgressDTO>())
            {
                if (chapterProgress.Chapter != null)
                    ProtectLessons(chapterProgress.Chapter.Lessons, signer);

                foreach (var lessonProgress in chapterProgress.LessonProgresses ?? new List<LessonProgressDTO>())
                {
                    if (lessonProgress.Lesson != null)
                        lessonProgress.Lesson.VideoUrl = signer.Protect(lessonProgress.Lesson.VideoUrl);
                }
            }

            return enrollment;
        }

        private static void ProtectChapters(IEnumerable<ChapterDTO>? chapters, IMediaUrlSigner signer)
        {
            foreach (var chapter in chapters ?? Enumerable.Empty<ChapterDTO>())
                ProtectLessons(chapter.Lessons, signer);
        }

        private static void ProtectLessons(IEnumerable<LessonDTO>? lessons, IMediaUrlSigner signer)
        {
            foreach (var lesson in lessons ?? Enumerable.Empty<LessonDTO>())
                lesson.VideoUrl = signer.Protect(lesson.VideoUrl);
        }
    }
}
