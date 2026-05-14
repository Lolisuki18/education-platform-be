using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class EnrollmentSeeder
    {
        public const decimal PLATFORM_COMMISSION_RATE = 0.15m; // 15% platform fee

        public static async Task<EnrollmentSeedResult> SeedAsync(
            EducationPlatformDBContext context,
            List<Guid> studentIds,
            Dictionary<Guid, decimal?> coursePrices)
        {
            var result = new EnrollmentSeedResult();

            if (await context.Set<Enrollment>().AnyAsync() ||
                await context.Set<Order>().AnyAsync())
            {
                return result;
            }

            var random = Random.Shared;

            var orders = new List<Order>();
            var enrollments = new List<Enrollment>();

            var uniquePairs = new HashSet<string>();

            int targetCount = 2000;
            int createdCount = 0;

            var courseIds = coursePrices.Keys.ToList();

            // 3-year range (2021 ? now)
            var endDate = DateTime.Now;
            var startDate = endDate.AddYears(-3);

            while (createdCount < targetCount)
            {
                var studentId = studentIds[random.Next(studentIds.Count)];
                var courseId = courseIds[random.Next(courseIds.Count)];

                var key = $"{studentId}-{courseId}";
                if (!uniquePairs.Add(key))
                    continue;

                var price = coursePrices[courseId];
                decimal totalPrice = price ?? 0;

                var commission = price.HasValue
                    ? Commission.Create(PLATFORM_COMMISSION_RATE, totalPrice)
                    : Commission.Create(0m, 0m); // free course

                // Order created date spread
                var createdAt = RandomDateBetween(random, startDate, endDate.AddMinutes(-1));

                var order = new Order(
                    Guid.NewGuid(),
                    commission,
                    studentId,
                    courseId,
                    createdAt
                );

                // Simulate payment (80% success)
                //if (random.NextDouble() > 0.2)
                //{
                    var paidAt = RandomDateBetween(random, createdAt, endDate);
                    order.StudentPaid(paidAt);
                //}

                orders.Add(order);
                result.OrderIds.Add(order.OrderID);

                // Enrollment if paid (Pending in your domain)
                if (order.Status == OrderStatus.Pending)
                {
                    var enrolledAt = RandomDateBetween(
                        random,
                        createdAt,
                        order.PaidAt ?? endDate
                    );

                    var enrollment = new Enrollment(
                        Guid.NewGuid(),
                        studentId,
                        courseId,
                        enrolledAt
                    );

                    if (random.Next(0, 15) == 0)
                    {
                        var completedAt = RandomDateBetween(
                            random,
                            enrolledAt,
                            endDate
                        );

                        enrollment.CompleteEnrollment(completedAt);
                    }

                    enrollments.Add(enrollment);
                    result.EnrollmentIds.Add(enrollment.EnrollmentID);
                }

                createdCount++;
            }

            context.AddRange(orders);
            context.AddRange(enrollments);

            return result;
        }

        // Helper: random date between range
        private static DateTime RandomDateBetween(Random random, DateTime start, DateTime end)
        {
            var range = (end - start).TotalSeconds;
            return start.AddSeconds(random.NextDouble() * range);
        }
    }
}
