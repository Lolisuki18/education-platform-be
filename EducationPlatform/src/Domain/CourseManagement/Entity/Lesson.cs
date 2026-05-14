using Domain.CourseManagement.Enum;
using Domain.DomainExceptions;

namespace Domain.CourseManagement.Entity
{
    public class Lesson
    {
        #region Attributes
        private readonly List<Quiz> quizzes = new();
        private readonly List<Material> materials = new();
        private readonly List<Assignment> assignments = new();
        #endregion

        #region Properties
        public Guid LessonID { get; private set; }
        public string Title { get; private set; }
        public string Objectives { get; private set; }
        public string Description { get; private set; }
        public string VideoUrl { get; private set; }
        public int Order { get; private set; }

        public Guid ChapterID { get; private set; }

        public IReadOnlyCollection<Quiz> Quizzes
        {
            get { return quizzes.AsReadOnly(); }
        }

        public IReadOnlyCollection<Material> Materials
        {
            get { return materials.AsReadOnly(); }
        }

        public IReadOnlyCollection<Assignment> Assignments
        {
            get { return assignments.AsReadOnly(); }
        }
        #endregion

        protected Lesson() { }

        public Lesson(
            Guid lessonId,
            string title,
            string objectives,
            string description,
            string videoUrl,
            int order,
            Guid chapterId)
        {
            if (lessonId == Guid.Empty)
                throw new DomainException(
                    "Lesson ID cannot be empty");

            if (chapterId == Guid.Empty)
                throw new DomainException(
                    "Chapter ID cannot be empty");

            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException(
                    "Lesson title is required");

            if (string.IsNullOrWhiteSpace(objectives))
                throw new DomainException(
                    "Lesson objectives is required");

            if (string.IsNullOrWhiteSpace(videoUrl))
                throw new DomainException(
                    "Lesson video URL is required");

            if (order <= 0)
                throw new DomainException(
                    "Lesson order must be greater than zero");

            LessonID = lessonId;
            Title = title.Trim();
            Objectives = objectives.Trim();
            Description = description.Trim();
            VideoUrl = videoUrl.Trim();
            Order = order;
            ChapterID = chapterId;
        }

        #region Methods
        public Quiz AddQuiz(string question, string? note)
        {
            var quiz = new Quiz(Guid.NewGuid(), question, note, LessonID);
            quizzes.Add(quiz);
            return quiz;
        }

        public Material AddMaterial(string name, string description, string url, MaterialType type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Material name is required");

            if (string.IsNullOrWhiteSpace(url))
                throw new DomainException("Material URL is required");

            var material = new Material(Guid.NewGuid(), name.Trim(), description.Trim(), url.Trim(), type, LessonID);
            materials.Add(material);
            return material;
        }

        public Assignment AddAssignment(string title, string description, int maxScore)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Assignment title is required");

            if (maxScore <= 0)
                throw new DomainException("Assignment max score must be greater than zero");

            var assignment = new Assignment(Guid.NewGuid(), title.Trim(), description?.Trim() ?? "", maxScore, LessonID);
            assignments.Add(assignment);
            return assignment;
        }
        #endregion
    }
}

