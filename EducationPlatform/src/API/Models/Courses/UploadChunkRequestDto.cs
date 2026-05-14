using Microsoft.AspNetCore.Http;

namespace API.Models.Courses
{
    public class UploadChunkRequestDto
    {
        public IFormFile Chunk { get; set; } = null!;
        public string UploadId { get; set; } = null!;
        public int Index { get; set; }
    }
}
