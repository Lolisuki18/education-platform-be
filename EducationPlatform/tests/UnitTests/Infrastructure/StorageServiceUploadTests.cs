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

        [Fact]
        public async Task OwnerCanSendChunksAndCompleteTheUpload()
        {
            var service = CreateService();
            var uploadId = Guid.NewGuid().ToString();

            await service.SaveChunkAsync(Bytes(10), uploadId, 0, _teacher, CancellationToken.None);
            await service.SaveChunkAsync(Bytes(10), uploadId, 1, _teacher, CancellationToken.None);
            var path = await service.CompleteUploadAsync(uploadId, "mp4", _teacher, CancellationToken.None);

            path.Should().Be($"videos/{uploadId}.mp4");
            new FileInfo(Path.Combine(_root, "videos", $"{uploadId}.mp4")).Length.Should().Be(20);
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
