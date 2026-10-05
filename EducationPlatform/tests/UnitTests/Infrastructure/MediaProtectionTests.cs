using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using API.Middlewares;
using Application.Features.Courses.CreateCourse;
using Application.Options;
using Domain.Common;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class HmacMediaUrlSignerTests
    {
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        private HmacMediaUrlSigner Create(string? mediaKey = null, int lifetimeMinutes = 240, string jwtKey = "0123456789012345678901234567890123456789") =>
            new(Options.Create(new MediaOptions { SigningKey = mediaKey, UrlLifetimeMinutes = lifetimeMinutes }),
                Options.Create(new JwtOptions { SecretKey = jwtKey }),
                _time);

        private static (long Expires, string Signature) Parse(string signedUrl)
        {
            var query = System.Web.HttpUtility.ParseQueryString(signedUrl[(signedUrl.IndexOf('?') + 1)..]);
            return (long.Parse(query["exp"]!), query["sig"]!);
        }

        [Theory]
        [InlineData("videos/lesson.mp4")]
        [InlineData("/videos/lesson.mp4")]
        [InlineData("media/videos/lesson.mp4")]
        [InlineData("Videos/lesson.mp4")]
        public void PlatformVideos_GetAnExpiryAndASignature(string url)
        {
            var signed = Create().Protect(url);

            signed.Should().StartWith(url + "?exp=");
            signed.Should().Contain("&sig=");
        }

        [Theory]
        [InlineData("https://res.cloudinary.com/demo/video/upload/a.mp4")]
        [InlineData("2026/06/thumbnail.png")]
        [InlineData("https://example.com/videos/a.mp4")]
        [InlineData("")]
        public void OtherUrls_AreLeftAlone(string url)
        {
            Create().Protect(url).Should().Be(url);
        }

        [Fact]
        public void ASignedLink_ShouldVerifyForItsOwnPath()
        {
            var signer = Create();
            var (expires, signature) = Parse(signer.Protect("videos/lesson.mp4"));

            signer.IsValid("videos/lesson.mp4", expires, signature).Should().BeTrue();
        }

        [Fact]
        public void TheSameFile_ShouldVerifyWhateverWayItsPathIsSpelled()
        {
            var signer = Create();
            var (expires, signature) = Parse(signer.Protect("/videos/lesson.mp4"));

            signer.IsValid("videos/lesson.mp4", expires, signature).Should().BeTrue();
            signer.IsValid("//videos//lesson.mp4", expires, signature).Should().BeTrue();
            signer.IsValid("media/videos/lesson.mp4", expires, signature).Should().BeTrue();
        }

        [Fact]
        public void ASignature_ShouldNotWorkForAnotherFile()
        {
            var signer = Create();
            var (expires, signature) = Parse(signer.Protect("videos/lesson.mp4"));

            signer.IsValid("videos/other.mp4", expires, signature).Should().BeFalse();
        }

        [Fact]
        public void ChangingTheExpiry_InvalidatesTheSignature()
        {
            var signer = Create();
            var (expires, signature) = Parse(signer.Protect("videos/lesson.mp4"));

            signer.IsValid("videos/lesson.mp4", expires + 3600, signature).Should().BeFalse();
        }

        [Fact]
        public void ALink_StopsWorkingOnceItExpires()
        {
            var signer = Create(lifetimeMinutes: 10);
            var (expires, signature) = Parse(signer.Protect("videos/lesson.mp4"));

            _time.Advance(TimeSpan.FromMinutes(9));
            signer.IsValid("videos/lesson.mp4", expires, signature).Should().BeTrue();

            _time.Advance(TimeSpan.FromMinutes(2));
            signer.IsValid("videos/lesson.mp4", expires, signature).Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-signature")]
        public void GarbageSignatures_AreRejected(string signature)
        {
            Create().IsValid("videos/lesson.mp4", _time.GetUtcNow().ToUnixTimeSeconds() + 100, signature).Should().BeFalse();
        }

        [Fact]
        public void ADifferentKey_CannotForgeLinks()
        {
            var attacker = Create(jwtKey: "ffffffffffffffffffffffffffffffffffffffff");
            var (expires, signature) = Parse(attacker.Protect("videos/lesson.mp4"));

            Create().IsValid("videos/lesson.mp4", expires, signature).Should().BeFalse();
        }

        [Fact]
        public void AnExplicitSigningKey_ReplacesTheJwtDerivedOne()
        {
            var withMediaKey = Create(mediaKey: "a-dedicated-media-signing-key-0000000000");
            var (expires, signature) = Parse(withMediaKey.Protect("videos/lesson.mp4"));

            Create().IsValid("videos/lesson.mp4", expires, signature).Should().BeFalse();
            withMediaKey.IsValid("videos/lesson.mp4", expires, signature).Should().BeTrue();
        }

        [Fact]
        public void AnExistingQueryString_IsExtended()
        {
            Create().Protect("videos/lesson.mp4?download=1").Should().Contain("?download=1&exp=");
        }
    }

    public class ProtectedMediaMiddlewareTests
    {
        private static async Task<(int Status, bool ReachedNext)> Send(string path, string query = "")
        {
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
            var signer = new HmacMediaUrlSigner(
                Options.Create(new MediaOptions()),
                Options.Create(new JwtOptions { SecretKey = "0123456789012345678901234567890123456789" }),
                time);

            var reached = false;
            var middleware = new ProtectedMediaMiddleware(_ => { reached = true; return Task.CompletedTask; });

            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.QueryString = new QueryString(query);
            context.Response.Body = new System.IO.MemoryStream();

            await middleware.InvokeAsync(context, signer);
            return (context.Response.StatusCode, reached);
        }

        private static string ValidQuery(string path)
        {
            var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
            var signer = new HmacMediaUrlSigner(
                Options.Create(new MediaOptions()),
                Options.Create(new JwtOptions { SecretKey = "0123456789012345678901234567890123456789" }),
                time);
            var signed = signer.Protect(path);
            return signed[signed.IndexOf('?')..];
        }

        [Fact]
        public async Task VideoWithoutASignature_IsForbidden()
        {
            var (status, reached) = await Send("/media/videos/lesson.mp4");

            status.Should().Be((int)HttpStatusCode.Forbidden);
            reached.Should().BeFalse();
        }

        [Fact]
        public async Task VideoWithAValidSignature_IsServed()
        {
            var (status, reached) = await Send("/media/videos/lesson.mp4", ValidQuery("videos/lesson.mp4"));

            reached.Should().BeTrue();
            status.Should().Be(200);
        }

        [Fact]
        public async Task VideoWithTheSignatureOfAnotherFile_IsForbidden()
        {
            var (status, reached) = await Send("/media/videos/other.mp4", ValidQuery("videos/lesson.mp4"));

            status.Should().Be((int)HttpStatusCode.Forbidden);
            reached.Should().BeFalse();
        }

        [Theory]
        [InlineData("/media//videos/lesson.mp4")]
        [InlineData("/media/Videos/lesson.mp4")]
        [InlineData("/media/media/videos/lesson.mp4")]
        public async Task OtherSpellingsOfTheVideoFolder_AreProtectedToo(string path)
        {
            var (status, reached) = await Send(path);

            status.Should().Be((int)HttpStatusCode.Forbidden);
            reached.Should().BeFalse();
        }

        [Theory]
        [InlineData("/media/2026/06/thumbnail.png")]
        [InlineData("/api/courses")]
        [InlineData("/media/videosfake/a.mp4")]
        public async Task EverythingElse_PassesThrough(string path)
        {
            var (_, reached) = await Send(path);

            reached.Should().BeTrue();
        }
    }

    public class SafeUrlTests
    {
        [Theory]
        [InlineData("videos/lesson.mp4")]
        [InlineData("/videos/lesson.mp4")]
        [InlineData("2026/06/thumbnail.png")]
        [InlineData("https://res.cloudinary.com/demo/video/upload/v1/a.mp4")]
        [InlineData("https://www.youtube.com/watch?v=abc")]
        public void SafeValues_AreAccepted(string url)
        {
            SafeUrl.IsSafe(url).Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("javascript:alert(1)")]
        [InlineData("JaVaScRiPt:alert(1)")]
        [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
        [InlineData("vbscript:msgbox(1)")]
        [InlineData("file:///etc/passwd")]
        [InlineData("http://example.com/a.mp4")]
        [InlineData("ftp://example.com/a.mp4")]
        [InlineData("//evil.example.com/a.mp4")]
        [InlineData("https://user:pass@example.com/a.mp4")]
        [InlineData("videos/../../etc/passwd")]
        [InlineData("..\\windows\\system.ini")]
        [InlineData("videos/a b.mp4")]
        [InlineData("videos/a\nb.mp4")]
        [InlineData("a:b")]
        public void DangerousValues_AreRefused(string? url)
        {
            SafeUrl.IsSafe(url).Should().BeFalse();
        }

        [Fact]
        public void OverlongValues_AreRefused()
        {
            SafeUrl.IsSafe("https://example.com/" + new string('a', 2000)).Should().BeFalse();
        }
    }

    public class SlugsTests
    {
        [Theory]
        [InlineData("Toán lớp 10 - Đại số", "toan-lop-10-dai-so")]
        [InlineData("  Hello,   World!  ", "hello-world")]
        [InlineData("Physics_101", "physics-101")]
        [InlineData("already-a-slug", "already-a-slug")]
        public void Normalize_ProducesLowercaseAsciiWords(string input, string expected)
        {
            Slugs.Normalize(input).Should().Be(expected);
        }

        [Fact]
        public void Create_PrefersTheGivenSlugAndFallsBackToTheTitle()
        {
            Slugs.Create("My Slug", "Some Title").Should().Be("my-slug");
            Slugs.Create(null, "Some Title").Should().Be("some-title");
            Slugs.Create("   ", "Some Title").Should().Be("some-title");
            Slugs.Create("!!!", "???").Should().Be("course");
        }

        [Fact]
        public void Normalize_KeepsRoomForAUniquenessSuffix()
        {
            var slug = Slugs.Normalize(new string('a', 500));

            slug.Length.Should().BeLessThan(Slugs.MaxLength);
            Slugs.WithSuffix(slug).Length.Should().BeLessThanOrEqualTo(Slugs.MaxLength);
        }

        [Fact]
        public void WithSuffix_ProducesDifferentValuesEachTime()
        {
            Slugs.WithSuffix("course").Should().NotBe(Slugs.WithSuffix("course"));
        }
    }

    public class CourseUrlValidationTests
    {
        private static CreateCourseCommand Command(string videoUrl, string materialUrl = "videos/doc.pdf") => new()
        {
            Title = "Course",
            Description = "Description",
            ThumbnailName = "thumb.png",
            Chapters =
            {
                new CreateChapterCommandDto
                {
                    Title = "Chapter",
                    Order = 1,
                    Lessons =
                    {
                        new CreateLessonCommandDto
                        {
                            Title = "Lesson",
                            Objectives = "o",
                            VideoUrl = videoUrl,
                            Order = 1,
                            Materials = { new CreateMaterialCommandDto { Name = "m", Url = materialUrl } }
                        }
                    }
                }
            }
        };

        [Fact]
        public void StoragePathsAndHttpsLinks_Pass()
        {
            new CreateCourseCommandValidator().Validate(Command("videos/a.mp4", "https://example.com/doc.pdf")).IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<script>")]
        [InlineData("http://example.com/a.mp4")]
        public void ScriptAndPlainHttpLinks_AreRejectedForVideos(string url)
        {
            var result = new CreateCourseCommandValidator().Validate(Command(url));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("video URL"));
        }

        [Fact]
        public void UnsafeMaterialLinks_AreRejected()
        {
            var result = new CreateCourseCommandValidator().Validate(Command("videos/a.mp4", "javascript:alert(1)"));

            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Material URL"));
        }

        [Fact]
        public void WithAnAllowList_OnlyThoseHostsAndTheirSubdomainsPass()
        {
            var validator = new CreateCourseCommandValidator(Options.Create(new MediaOptions
            {
                AllowedExternalHosts = new[] { "cloudinary.com" }
            }));

            validator.Validate(Command("https://res.cloudinary.com/a.mp4")).IsValid.Should().BeTrue();
            validator.Validate(Command("https://cloudinary.com/a.mp4")).IsValid.Should().BeTrue();
            validator.Validate(Command("videos/a.mp4")).IsValid.Should().BeTrue(); // storage paths are always fine
            validator.Validate(Command("https://evil-cloudinary.com/a.mp4")).IsValid.Should().BeFalse();
            validator.Validate(Command("https://example.com/a.mp4")).IsValid.Should().BeFalse();
        }

        [Fact]
        public void OverlongSlugs_AreRejected()
        {
            var command = Command("videos/a.mp4");
            command.Slug = new string('a', Slugs.MaxLength + 1);

            new CreateCourseCommandValidator().Validate(command).IsValid.Should().BeFalse();
        }
    }
}
