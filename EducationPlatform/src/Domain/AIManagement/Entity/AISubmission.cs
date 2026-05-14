using Domain.DomainExceptions;

namespace Domain.AIManagement.Entity
{
    public class AISubmission
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid AISubmissionID { get; private set; }
        public string Answer { get; private set; }
        public bool IsCorrect { get; private set; }
        public string Feedback { get; private set; }
        public DateTime SubmittedAt { get; private set; }

        public Guid AIAssignmentID { get; private set; }
        #endregion

        protected AISubmission() { }

        public AISubmission(
            Guid aiSubmissionId,
            Guid aiAssignmentId,
            string answer,
            bool isCorrect,
            string feedback)
        {
            if (aiSubmissionId == Guid.Empty)
                throw new DomainException(
                    "AI submission ID cannot be empty");

            if (aiAssignmentId == Guid.Empty)
                throw new DomainException(
                    "AI assignment ID cannot be empty");

            AISubmissionID = aiSubmissionId;
            Answer = answer;
            IsCorrect = isCorrect;
            Feedback = feedback;
            SubmittedAt = DateTime.UtcNow;
            AIAssignmentID = aiAssignmentId;
        }

        #region Methods
        #endregion
    }
}

