using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Options;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class StorageServiceUploadTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "ep-storage-tests-" + Guid.NewGuid());
        private readonly Guid _teacher = Guid.NewGuid();

        public StorageServiceUploadTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private StorageService CreateService(UploadOptions? options = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[] { new System.Collections.Generic.KeyValuePair<string, string?>("Storage:RootPath", _root) })
                .Build();

            return new StorageService(configuration, NullLogger<StorageService>.Instance, Options.Create(options ?? new UploadOptions()));
        }

        private static MemoryStream Bytes(int length) => new(new byte[length]);

        // The first bytes of an MP4: 4 bytes of atom size, then the atom name "ftyp"
        private static readonly byte[] Mp4Start = { 0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D };

        private static MemoryStream Mp4Chunk(int totalLength)
        {
            var bytes = new byte[totalLength];
            Mp4Start.CopyTo(bytes, 0);
            return new MemoryStream(bytes);
        }

        [Fact]
        public async Task OwnerCanSendChunksAndCompleteTheUpload()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();

            await service.SaveChunkAsync(Mp4Chunk(12), uploadId, 0, _teacher, CancellationToken.None);
            await service.SaveChunkAsync(Bytes(8), uploadId, 1, _teacher, CancellationToken.None);
            var path = await service.CompleteUploadAsync(uploadId, "mp4", _teacher, CancellationToken.None);

            path.Should().Be($"videos/{uploadId}.mp4");
            new FileInfo(Path.Combine(_root, "videos", $"{uploadId}.mp4")).Length.Should().Be(20);
        }

        [Theory]
        [InlineData("mp4")]
        [InlineData("webm")]
        public async Task CompletingAnUpload_ThatIsNotThatKindOfVideo_IsRejectedAndLeavesNothingBehind(string extension)
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();

            // An HTML page is not a video, whatever extension the client gives it
            await service.SaveChunkAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>")), uploadId, 0, _teacher, CancellationToken.None);

            Func<Task> act = () => service.CompleteUploadAsync(uploadId, extension, _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
            File.Exists(Path.Combine(_root, "videos", $"{uploadId}.{extension}")).Should().BeFalse();
        }

        [Fact]
        public async Task CompletingAnUploadThatStartedAtASecondChunk_IsRejected()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();

            // Chunk 0 never arrived: what is left starts mid-file
            await service.SaveChunkAsync(Bytes(10), uploadId, 1, _teacher, CancellationToken.None);

            Func<Task> act = () => service.CompleteUploadAsync(uploadId, "mp4", _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task AWebmUpload_IsRecognisedByItsEbmlMarker()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();

            await service.SaveChunkAsync(new MemoryStream(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 1, 2 }), uploadId, 0, _teacher, CancellationToken.None);
            var path = await service.CompleteUploadAsync(uploadId, "webm", _teacher, CancellationToken.None);

            path.Should().Be($"videos/{uploadId}.webm");
        }

        [Fact]
        public async Task AnotherUser_CannotAddChunksToSomeoneElsesUpload()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();
            await service.SaveChunkAsync(Bytes(10), uploadId, 0, _teacher, CancellationToken.None);

            Func<Task> act = () => service.SaveChunkAsync(Bytes(10), uploadId, 1, Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task AnotherUser_CannotCompleteSomeoneElsesUpload()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();
            await service.SaveChunkAsync(Bytes(10), uploadId, 0, _teacher, CancellationToken.None);

            Func<Task> act = () => service.CompleteUploadAsync(uploadId, "mp4", Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<ForbiddenException>();
        }

        [Fact]
        public async Task CompletingAnUnknownUpload_ShouldBeNotFound()
        {
            var service = CreateService();

            Func<Task> act = () => service.CompleteUploadAsync(Guid.NewGuid().ToString(), "mp4", _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(5)]
        public async Task ChunkIndexOutsideTheAllowedRange_ShouldBeRejected(int index)
        {
            var service = CreateService(new UploadOptions { MaxChunksPerUpload = 5 });

            Func<Task> act = () => service.SaveChunkAsync(Bytes(1), Guid.NewGuid().ToString(), index, _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task OversizedChunk_ShouldBeRejected()
        {
            var service = CreateService(new UploadOptions { MaxChunkBytes = 100 });

            Func<Task> act = () => service.SaveChunkAsync(Bytes(101), Guid.NewGuid().ToString(), 0, _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task UploadBeyondTheTotalLimit_ShouldBeRejected()
        {
            var service = CreateService(new UploadOptions { MaxChunkBytes = 100, MaxTotalBytesPerUpload = 150 });
            var uploadId = Guid.NewGuid().ToString();
            await service.SaveChunkAsync(Bytes(100), uploadId, 0, _teacher, CancellationToken.None);

            Func<Task> act = () => service.SaveChunkAsync(Bytes(100), uploadId, 1, _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
        }

        [Fact]
        public async Task ResendingAChunk_ShouldNotCountTwice()
        {
            var service = CreateService(new UploadOptions { MaxChunkBytes = 100, MaxTotalBytesPerUpload = 150 });
            var uploadId = Guid.NewGuid().ToString();

            await service.SaveChunkAsync(Bytes(100), uploadId, 0, _teacher, CancellationToken.None);
            Func<Task> retry = () => service.SaveChunkAsync(Bytes(100), uploadId, 0, _teacher, CancellationToken.None);

            await retry.Should().NotThrowAsync();
        }

        [Fact]
        public async Task InvalidUploadId_ShouldBeRejected()
        {
            var service = CreateService();

            Func<Task> act = () => service.SaveChunkAsync(Bytes(1), "../../etc", 0, _teacher, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
        }
    }
}
