namespace Application.Interface
{
    /// <summary>
    /// Videos uploaded to the platform are not public: a lesson video is only reachable through a URL that carries
    /// an expiry and a signature, handed out to people who may watch it (enrolled students, the owning teacher, admins).
    /// </summary>
    public interface IMediaUrlSigner
    {
        /// <summary>
        /// Returns <paramref name="url"/> with an expiry and signature appended when it points at a protected
        /// platform video; any other URL (external hosts, thumbnails, ...) is returned unchanged.
        /// </summary>
        string Protect(string url);

        /// <summary>Checks the signature and expiry of a request for <paramref name="relativePath"/> (e.g. <c>videos/a.mp4</c>).</summary>
        bool IsValid(string relativePath, long expires, string signature);
    }
}
