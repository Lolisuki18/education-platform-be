using Application.Options;
using Domain.Common;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
    {
        public CreateCourseCommandValidator(IOptions<MediaOptions>? media = null)
        {
            var allowedHosts = media?.Value.AllowedExternalHosts ?? Array.Empty<string>();

            RuleFor(v => v.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

            RuleFor(v => v.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be a positive value.");

            RuleFor(v => v.Description)
                .NotEmpty().WithMessage("Description is required.");

            // An uploaded thumbnail names itself; a name is only needed when the course points at an existing image
            RuleFor(v => v.ThumbnailName)
                .NotEmpty().When(v => v.ThumbnailFileStream == null).WithMessage("Thumbnail is required.");

            RuleFor(v => v.Slug)
                .MaximumLength(Slugs.MaxLength).WithMessage($"Slug must not exceed {Slugs.MaxLength} characters.");

            // Links are typed by teachers and rendered by the frontend, so only safe ones are stored
            RuleForEach(v => v.Chapters).ChildRules(chapter =>
            {
                chapter.RuleForEach(c => c.Lessons).ChildRules(lesson =>
                {
                    lesson.RuleFor(l => l.VideoUrl)
                        .Must(url => SafeUrl.IsSafe(url))
                            .WithMessage("Lesson video URL must be a storage path or an https URL.")
                        .Must(url => IsHostAllowed(url, allowedHosts))
                            .WithMessage("Lesson video URL points to a host that is not allowed.");

                    lesson.RuleForEach(l => l.Materials).ChildRules(material =>
                    {
                        material.RuleFor(m => m.Url)
                            .Must(url => SafeUrl.IsSafe(url))
                                .WithMessage("Material URL must be a storage path or an https URL.")
                            .Must(url => IsHostAllowed(url, allowedHosts))
                                .WithMessage("Material URL points to a host that is not allowed.");
                    });
                });
            });
        }

        /// <summary>Storage paths are always fine; an external URL must be on the allow-list (subdomains count) when one is set.</summary>
        internal static bool IsHostAllowed(string? url, string[] allowedHosts)
        {
            if (allowedHosts.Length == 0 || string.IsNullOrWhiteSpace(url))
                return true;

            var host = SafeUrl.HostOf(url);
            if (host == null)
                return true;

            return allowedHosts.Any(allowed =>
                host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
        }
    }
}
