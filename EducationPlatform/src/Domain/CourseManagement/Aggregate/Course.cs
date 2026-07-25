using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.ValueObject;
using Domain.DomainExceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.Common;

namespace Domain.CourseManagement.Aggregate
{
    public class Course : BaseEntity
    {
        #region Attributes
        private readonly List<ViolatedPolicy> violatedPolicies = new();
        private readonly List<Chapter> chapters = new();
        #endregion

        #region Properties
        public Guid CourseID { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public CourseStatus Status { get; private set; }
        public CoursePrice Price { get; private set; }
        public string? ThumbnailName { get; private set; }
        public string Slug { get; private set; }
        public string? Prerequisites { get; private set; }
        public string? LearningOutcomes { get; private set; }
        public DateTime? RejectedAt { get; private set; }
        public string? AdminNote { get; private set; }
        public DateTime? PublishedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Guid TeacherID { get; private set; }
        public Guid GradeID { get; private set; }
        public Guid SubjectID { get; private set; }

        public User Teacher { get; private set; } = null!;
        public Grade Grade { get; private set; } = null!;
        public Subject Subject { get; private set; } = null!;

        public IReadOnlyCollection<ViolatedPolicy> ViolatedPolicies
        {
            get { return violatedPolicies.AsReadOnly(); }
        }

        public IReadOnlyCollection<Chapter> Chapters
        {
            get { return chapters.AsReadOnly(); }
        }
        #endregion

        protected Course() { }

        public Course(
            Guid courseId,
            string title,
            string description,
            decimal? price,
            string thumbnailName,
            string? slug,
            string prerequisites,
            string learningOutcomes,
            Guid teacherId,
            Guid gradeId,
            Guid subjectId,
            DateTime? createdAt)
        {
            if (courseId == Guid.Empty)
                throw new DomainException("Course ID cannot be empty");

            if (teacherId == Guid.Empty)
                throw new DomainException("Teacher ID cannot be empty");

            if (gradeId == Guid.Empty)
                throw new DomainException("Grade ID cannot be empty");

            if (subjectId == Guid.Empty)
                throw new DomainException("Subject ID cannot be empty");

            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Title is required");

            if (string.IsNullOrWhiteSpace(description))
                throw new DomainException("Description is required");

            CourseID = courseId;
            Title = title.Trim();
            Description = description.Trim();
            Status = CourseStatus.InReview;
            Price = price.HasValue ? CoursePrice.Paid(price.Value) : CoursePrice.Free();
            ThumbnailName = thumbnailName;
            Slug = slug ?? "Default";
            Prerequisites = prerequisites?.Trim();
            LearningOutcomes = learningOutcomes?.Trim();
            TeacherID = teacherId;
            GradeID = gradeId;
            SubjectID = subjectId;
            CreatedAt = createdAt ?? DateTime.UtcNow;

            AddDomainEvent(new Events.CourseCreatedEvent(CourseID, Title));
        }

        public void UpdateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Title is required");
            Title = title.Trim();
        }

        #region Methods
        public Chapter AddChapter(string title, string description, int order)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Chapter title is required");

            var chapter = new Chapter(
                Guid.NewGuid(),
                description,
                title.Trim(),
                order,
                CourseID
            );

            chapters.Add(chapter);

            return chapter;
        }

        public List<ViolatedPolicy> ReviewCourse(
            List<Guid>? violatedPolicyIds,
            List<(Guid violatedChapterId, string adminNote)>? violatedChapterNotes,
            string? adminNote,
            Guid adminId)
        {
            var hasViolatedChapters = violatedChapterNotes?.Any() == true;
            var hasViolatedPolicies = violatedPolicyIds?.Any() == true;

            if (hasViolatedChapters && !hasViolatedPolicies)
                throw new DomainException(
                    "A violated chapter must be associated with at least one violated policy.");

            AdminNote = adminNote;

            // Reset all chapters
            foreach (var chapter in chapters)
            {
                chapter.NoteAsNonViolated();
            }

            // Mark violated chapters
            if (hasViolatedChapters)
            {
                foreach (var (chapterId, note) in violatedChapterNotes!)
                {
                    var chapter = chapters.FirstOrDefault(l => l.ChapterID == chapterId)
                                 ?? throw new DomainException(
                                     $"Violated chapter with ID: {chapterId} is not found");

                    chapter.NoteAsViolated(note);
                }
            }

            // Clear existing violated policies
            violatedPolicies.Clear();

            // Add new violated policies if any

            if (hasViolatedPolicies)
            {
                foreach (var policyId in violatedPolicyIds!)
                {
                    violatedPolicies.Add(new ViolatedPolicy(Guid.NewGuid(), policyId, CourseID));
                }

                RejectedAt = DateTime.UtcNow;
                Status = CourseStatus.Rejected;
            }
            else
            {
                PublishedAt = DateTime.UtcNow;
                Status = CourseStatus.Published;
            }
            AddDomainEvent(new Events.CourseReviewedEvent(CourseID, Title, Status, adminId, TeacherID));

            return violatedPolicies;
        }

        public void RejectByComplaint(string adminNote)
        {
            if (string.IsNullOrWhiteSpace(adminNote))
                throw new DomainException("Admin note is required.");

            AdminNote = adminNote;

            foreach (var chapter in chapters)
            {
                chapter.NoteAsNonViolated();
            }

            violatedPolicies.Clear();

            Status = CourseStatus.Rejected;
            RejectedAt = DateTime.UtcNow;
        }

        public void MarkAsPublished(DateTime publishedAt)
        {
            Status = CourseStatus.Published;
            PublishedAt = publishedAt;
        }

        public void MarkAsRejected(DateTime rejectedAt, string note)
        {
            Status = CourseStatus.Rejected;
            RejectedAt = rejectedAt;
            AdminNote = note;
        }
        #endregion
    }
}

