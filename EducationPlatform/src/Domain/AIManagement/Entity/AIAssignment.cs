using Domain.DomainExceptions;

namespace Domain.AIManagement.Entity
{
    public class AIAssignment
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid AIAssignmentID { get; private set; }
        public string Question { get; private set; }
        public string Guidance { get; private set; }

        public Guid SessionID { get; private set; }
        public Guid LessonID { get; private set; }

        public AISubmission AISubmission { get; private set; }
        #endregion

        protected AIAssignment() { }

        public AIAssignment(
            Guid aiAssignmentID,
            string question,
            string guidance,
            Guid sessionId,
            Guid lessonId)
        {
            if (aiAssignmentID == Guid.Empty)
                throw new DomainException(
                    "AI assignment ID cannot be empty");

            if (sessionId == Guid.Empty)
                throw new DomainException(
                    "Session ID cannot be empty");

            if (lessonId == Guid.Empty)
                throw new DomainException(
                    "Lession ID cannot be empty");

            AIAssignmentID = aiAssignmentID;
            Question = question;
            Guidance = guidance;
            SessionID = sessionId;
            LessonID = lessonId;
        }

        #region Methods
        #endregion
    }
}

