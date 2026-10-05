namespace Application.Common
{
    /// <summary>Keeps client-supplied paging values inside sane bounds, so a request cannot ask for a million rows.</summary>
    public static class Paging
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 100;

        public static int NormalizePageIndex(int value) => value < 1 ? 1 : value;

        public static int NormalizePageSize(int value) =>
            value <= 0 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}
