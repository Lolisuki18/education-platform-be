namespace Domain.CourseManagement.Entity
{
    public class Assignment
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid AssignmentID { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public int MaxScore { get; private set; }

        public Guid LessonID { get; private set; }
        #endregion

        protected Assignment() { }

        public Assignment(
            Guid assignmentId,
            string title,
            string description,
            int maxScore,
            Guid lessonId)
        {
            AssignmentID = assignmentId;
            Title = title;
            Description = description;
            MaxScore = maxScore;
            LessonID = lessonId;
        }

        #region Methods
        #endregion
    }
}

