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
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
