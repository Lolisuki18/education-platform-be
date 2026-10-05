using Application.Results;
using MediatR;
using System;
using System.Collections.Generic;

namespace Application.Features.Courses.Queries.GetCourses
{
    public class GetCoursesQuery : IRequest<List<CourseDTO>>
    {
        public string? Title { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string? TeacherName { get; set; } = string.Empty;
        public string? GradeName { get; set; } = string.Empty;
        public string? SubjectName { get; set; } = string.Empty;
        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Application.Common.Paging.NormalizePageIndex(value);
        }
        private int _pageSize = Application.Common.Paging.DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Application.Common.Paging.NormalizePageSize(value);
        }
    }
}
