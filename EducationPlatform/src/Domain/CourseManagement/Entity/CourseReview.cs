using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;

namespace Domain.CourseManagement.Entity
{
    public class CourseReview
    {
        #region Attributes
        #endregion

        #region Properties
         public Guid Id { get; private set; }

        public Guid CourseID { get; private set; }

        public Guid StudentID { get; private set; }

        public float Rating { get; private set; }

        public string? Comment { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? UpdatedAt { get; private set; }

        public DateTime? DeleteAt { get; private set; }
 
        public virtual Course Course { get; private set; } = null!;
        public virtual User Student { get; private set; } = null!;
        #endregion

        protected CourseReview() { }

       public CourseReview(Guid courseId, Guid studentId, float rating, string? comment)
        {
            Id = Guid.NewGuid();
            CourseID = courseId;
            StudentID = studentId;
            Rating = rating;
            Comment = comment;
            CreatedAt = DateTime.UtcNow;
        }
        #region Method
        public void UpdateReview(float rating, string? comment)
        {
            Rating = rating;
            Comment = string.IsNullOrWhiteSpace(comment) ? Comment : comment;
        }

        public void DeleteReview()
        {
            DeleteAt = DateTime.UtcNow;
        }
        #endregion
    }
}
