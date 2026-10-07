using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Events;
using Domain.CourseManagement.ValueObject;
using Domain.Exceptions;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace UnitTests.DomainTests.CourseManagement
{
    public class CourseTests
    {
        // ======================= COURSE PRICE TESTS =======================
        [Fact]
        public void CoursePrice_Free_ShouldCreateFreePrice()
        {
            var price = CoursePrice.Free();
            price.Amount.Should().Be(0);
            price.IsFree().Should().BeTrue();
        }

        [Fact]
        public void CoursePrice_Paid_ShouldCreatePaidPrice()
        {
            var price = CoursePrice.Paid(150m);
            price.Amount.Should().Be(150m);
            price.IsFree().Should().BeFalse();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public void CoursePrice_PaidWithInvalidAmount_ShouldThrowDomainException(decimal amount)
        {
            Action act = () => CoursePrice.Paid(amount);
            act.Should().Throw<DomainException>()
                .WithMessage("Course price must be greater than zero");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0)]
        public void Course_WithNoPriceOrAPriceOfZero_IsFree(int? amount)
        {
            var course = new Course(Guid.NewGuid(), "Free course", "Desc", amount, "t.png", null, "none", "out",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

            course.Price.IsFree().Should().BeTrue();
        }

        [Fact]
        public void Course_WithNegativePrice_Throws()
        {
            Action act = () => new Course(Guid.NewGuid(), "Course", "Desc", -5m, "t.png", null, "none", "out",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

            act.Should().Throw<DomainException>().WithMessage("Course price must be greater than zero");
        }

        [Fact]
        public void CoursePrice_EqualsAndHashCode_ShouldBehaveCorrectly()
        {
            var price1 = CoursePrice.Paid(100m);
            var price2 = CoursePrice.Paid(100m);
            var price3 = CoursePrice.Paid(150m);

            price1.Equals(price2).Should().BeTrue();
            price1.Equals(price3).Should().BeFalse();
            price1.GetHashCode().Should().Be(price2.GetHashCode());
        }

        // ======================= COURSE TESTS =======================
        [Fact]
        public void CourseConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new Course(Guid.Empty, "Title", "Desc", null, "thumb.png", null, "Prereq", "Outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act1.Should().Throw<DomainException>().WithMessage("Course ID cannot be empty");

            Action act2 = () => new Course(Guid.NewGuid(), "Title", "Desc", null, "thumb.png", null, "Prereq", "Outcomes", Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act2.Should().Throw<DomainException>().WithMessage("Teacher ID cannot be empty");

            Action act3 = () => new Course(Guid.NewGuid(), "Title", "Desc", null, "thumb.png", null, "Prereq", "Outcomes", Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);
            act3.Should().Throw<DomainException>().WithMessage("Grade ID cannot be empty");

            Action act4 = () => new Course(Guid.NewGuid(), "Title", "Desc", null, "thumb.png", null, "Prereq", "Outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);
            act4.Should().Throw<DomainException>().WithMessage("Subject ID cannot be empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CourseConstructor_InvalidTitle_ShouldThrowDomainException(string? title)
        {
            Action act = () => new Course(Guid.NewGuid(), title!, "Desc", null, "thumb.png", null, "Prereq", "Outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Title is required");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CourseConstructor_InvalidDescription_ShouldThrowDomainException(string? desc)
        {
            Action act = () => new Course(Guid.NewGuid(), "Title", desc!, null, "thumb.png", null, "Prereq", "Outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Description is required");
        }

        [Fact]
        public void CourseConstructor_ValidArguments_ShouldCreateSuccessfullyAndRaiseEvent()
        {
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var gradeId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            var course = new Course(courseId, "  Course Title  ", "  Course Description  ", 100m, "thumb.png", "course-slug", "Prereq", "Outcomes", teacherId, gradeId, subjectId, createdAt);

            course.CourseID.Should().Be(courseId);
            course.Title.Should().Be("Course Title");
            course.Description.Should().Be("Course Description");
            course.Status.Should().Be(CourseStatus.InReview);
            course.Price.Amount.Should().Be(100m);
            course.ThumbnailName.Should().Be("thumb.png");
            course.Slug.Should().Be("course-slug");
            course.Prerequisites.Should().Be("Prereq");
            course.LearningOutcomes.Should().Be("Outcomes");
            course.TeacherID.Should().Be(teacherId);
            course.GradeID.Should().Be(gradeId);
            course.SubjectID.Should().Be(subjectId);
            course.CreatedAt.Should().Be(createdAt);

            course.DomainEvents.Should().HaveCount(1);
            var domainEvent = course.DomainEvents.First().Should().BeOfType<CourseCreatedEvent>().Subject;
            domainEvent.CourseId.Should().Be(courseId);
            domainEvent.Title.Should().Be("Course Title");
        }

        [Fact]
        public void Course_AddChapter_ShouldAddAndReturnChapter()
        {
            var course = CreateTestCourse();

            var chapter = course.AddChapter("  Chapter 1  ", "Description", 1);

            chapter.Should().NotBeNull();
            chapter.Title.Should().Be("Chapter 1");
            chapter.Description.Should().Be("Description");
            chapter.Order.Should().Be(1);
            chapter.CourseID.Should().Be(course.CourseID);
            course.Chapters.Should().Contain(chapter);
        }

        [Fact]
        public void Course_AddChapterWithInvalidTitle_ShouldThrowDomainException()
        {
            var course = CreateTestCourse();
            Action act = () => course.AddChapter("", "Description", 1);
            act.Should().Throw<DomainException>().WithMessage("Chapter title is required");
        }

        [Fact]
        public void Course_ReviewCourse_WithViolatedChapterButNoViolatedPolicies_ShouldThrowDomainException()
        {
            var course = CreateTestCourse();
            var chapter = course.AddChapter("Chapter 1", "Desc", 1);
            var adminId = Guid.NewGuid();

            var violatedChapterNotes = new List<(Guid, string)> { (chapter.ChapterID, "Inappropriate Content") };

            Action act = () => course.ReviewCourse(null, violatedChapterNotes, "Failed", adminId);

            act.Should().Throw<DomainException>()
                .WithMessage("A violated chapter must be associated with at least one violated policy.");
        }

        [Fact]
        public void Course_ReviewCourse_WithNoViolations_ShouldPublishSuccessfully()
        {
            var course = CreateTestCourse();
            var adminId = Guid.NewGuid();

            var violatedPolicies = course.ReviewCourse(null, null, "Approved note", adminId);

            violatedPolicies.Should().BeEmpty();
            course.Status.Should().Be(CourseStatus.Published);
            course.PublishedAt.Should().NotBeNull();
            course.RejectedAt.Should().BeNull();
            course.AdminNote.Should().Be("Approved note");

            course.DomainEvents.Should().HaveCount(2); // CourseCreatedEvent, CourseReviewedEvent
            var reviewEvent = (CourseReviewedEvent)course.DomainEvents.Last();
            reviewEvent.CourseID.Should().Be(course.CourseID);
            reviewEvent.Outcome.Should().Be(CourseStatus.Published);
            reviewEvent.ReviewedByAdminID.Should().Be(adminId);
        }

        [Fact]
        public void Course_ReviewCourse_WithViolations_ShouldRejectAndAddViolatedPolicies()
        {
            var course = CreateTestCourse();
            var chapter = course.AddChapter("Chapter 1", "Desc", 1);
            var adminId = Guid.NewGuid();
            var policyId = Guid.NewGuid();

            var violatedPolicyIds = new List<Guid> { policyId };
            var violatedChapterNotes = new List<(Guid, string)> { (chapter.ChapterID, "Inappropriate") };

            var violatedPolicies = course.ReviewCourse(violatedPolicyIds, violatedChapterNotes, "Rejected note", adminId);

            violatedPolicies.Should().HaveCount(1);
            violatedPolicies.First().PolicyID.Should().Be(policyId);
            course.Status.Should().Be(CourseStatus.Rejected);
            course.RejectedAt.Should().NotBeNull();
            course.PublishedAt.Should().BeNull();
            course.AdminNote.Should().Be("Rejected note");

            chapter.IsViolated.Should().BeTrue();
            chapter.AdminNote.Should().Be("Inappropriate");
        }

        [Fact]
        public void Course_RejectByComplaint_ShouldClearViolationsAndMarkAsRejected()
        {
            var course = CreateTestCourse();
            var chapter = course.AddChapter("Chapter 1", "Desc", 1);
            chapter.NoteAsViolated("Old Violation Note");

            course.RejectByComplaint("Rejected due to spam complaint");

            course.Status.Should().Be(CourseStatus.Rejected);
            course.AdminNote.Should().Be("Rejected due to spam complaint");
            course.RejectedAt.Should().NotBeNull();
            course.ViolatedPolicies.Should().BeEmpty();
            chapter.IsViolated.Should().BeFalse();
        }

        [Fact]
        public void Course_RejectByComplaintWithEmptyNote_ShouldThrowDomainException()
        {
            var course = CreateTestCourse();
            Action act = () => course.RejectByComplaint("");
            act.Should().Throw<DomainException>().WithMessage("Admin note is required.");
        }

        [Fact]
        public void Course_MarkAsPublished_ShouldSetStatusAndPublishedAt()
        {
            var course = CreateTestCourse();
            var publishedAt = DateTime.UtcNow;

            course.MarkAsPublished(publishedAt);

            course.Status.Should().Be(CourseStatus.Published);
            course.PublishedAt.Should().Be(publishedAt);
        }

        [Fact]
        public void Course_MarkAsRejected_ShouldSetStatusAndRejectedAt()
        {
            var course = CreateTestCourse();
            var rejectedAt = DateTime.UtcNow;

            course.MarkAsRejected(rejectedAt, "Policy breach");

            course.Status.Should().Be(CourseStatus.Rejected);
            course.RejectedAt.Should().Be(rejectedAt);
            course.AdminNote.Should().Be("Policy breach");
        }

        // ======================= COMPLAINT TESTS =======================
        [Fact]
        public void ComplaintConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new Complaint(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Reason", null);
            act1.Should().Throw<DomainException>().WithMessage("Complaint ID cannot be empty");

            Action act2 = () => new Complaint(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Reason", null);
            act2.Should().Throw<DomainException>().WithMessage("Course ID cannot be empty");

            Action act3 = () => new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Reason", null);
            act3.Should().Throw<DomainException>().WithMessage("Student ID cannot be empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ComplaintConstructor_InvalidReason_ShouldThrowDomainException(string? reason)
        {
            Action act = () => new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reason!, null);
            act.Should().Throw<DomainException>().WithMessage("Complaint reason is required");
        }

        [Fact]
        public void ComplaintConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var complaintId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();

            var complaint = new Complaint(complaintId, courseId, studentId, "  Spam content  ", "evidence.png");

            complaint.ComplaintID.Should().Be(complaintId);
            complaint.CourseID.Should().Be(courseId);
            complaint.StudentID.Should().Be(studentId);
            complaint.Reason.Should().Be("Spam content");
            complaint.EvidenceImagePath.Should().Be("evidence.png");
            complaint.Status.Should().Be(ComplaintStatus.Pending);
            complaint.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Complaint_Approve_ShouldWorkAndRaiseEvent()
        {
            var complaint = new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason", null);

            complaint.Approve("Approved note");

            complaint.Status.Should().Be(ComplaintStatus.Approved);
            complaint.AdminNote.Should().Be("Approved note");
            complaint.ReviewedAt.Should().NotBeNull();

            complaint.DomainEvents.Should().HaveCount(1);
            var domainEvent = (ComplaintApprovedEvent)complaint.DomainEvents.First();
            domainEvent.ComplaintId.Should().Be(complaint.ComplaintID);
            domainEvent.CourseId.Should().Be(complaint.CourseID);
        }

        [Fact]
        public void Complaint_Approve_WhenNotPending_ShouldThrowDomainException()
        {
            var complaint = new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason", null);
            complaint.Approve("Done");

            Action act = () => complaint.Approve("Done again");
            act.Should().Throw<DomainException>().WithMessage("Only pending complaints can be approved.");
        }

        [Fact]
        public void Complaint_Reject_ShouldSetStatusToRejected()
        {
            var complaint = new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason", null);

            complaint.Reject("Rejected note");

            complaint.Status.Should().Be(ComplaintStatus.Rejected);
            complaint.AdminNote.Should().Be("Rejected note");
            complaint.ReviewedAt.Should().NotBeNull();
        }

        [Fact]
        public void Complaint_Reject_AfterItWasReviewed_ShouldThrow()
        {
            var complaint = new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason", null);
            complaint.Approve("Done");

            Action act = () => complaint.Reject("Changed my mind");

            act.Should().Throw<DomainException>().WithMessage("Only pending complaints can be rejected.");
            complaint.Status.Should().Be(ComplaintStatus.Approved);
        }

        // ======================= POLICY TESTS =======================
        [Fact]
        public void PolicyConstructor_EmptyId_ShouldThrowDomainException()
        {
            Action act = () => new Policy(Guid.Empty, "Policy Name");
            act.Should().Throw<DomainException>().WithMessage("Policy ID cannot be empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PolicyConstructor_InvalidName_ShouldThrowDomainException(string? name)
        {
            Action act = () => new Policy(Guid.NewGuid(), name!);
            act.Should().Throw<DomainException>().WithMessage("Policy name is required");
        }

        [Fact]
        public void PolicyConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var policyId = Guid.NewGuid();
            var policy = new Policy(policyId, "  Refund Policy  ");

            policy.PolicyID.Should().Be(policyId);
            policy.Name.Should().Be("Refund Policy");
            policy.IsActive.Should().BeTrue();
            policy.PolicyRules.Should().BeEmpty();
        }

        private Course CreateTestCourse()
        {
            return new Course(
                Guid.NewGuid(),
                "Title",
                "Description",
                100m,
                "thumb.png",
                "slug",
                "Prereq",
                "Outcomes",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow
            );
        }
    }
}
