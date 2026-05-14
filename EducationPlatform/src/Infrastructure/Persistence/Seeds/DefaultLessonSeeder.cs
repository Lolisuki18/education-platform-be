using Domain.AcademicManagement.Entity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seeds
{
    public static class DefaultLessonSeeder
    {
        public static async Task SeedAsync(
            EducationPlatformDBContext context,
            Dictionary<int, Guid> gradeIds,
            Dictionary<string, Guid> subjectIds)
        {
            if (await context.Set<DefaultLesson>().AnyAsync())
                return;

            var lessons = new List<DefaultLesson>();

            foreach (var gradePair in gradeIds)
            {
                int gradeNumber = gradePair.Key;
                Guid gradeId = gradePair.Value;

                foreach (var subjectPair in subjectIds)
                {
                    string subjectCode = subjectPair.Key;
                    Guid subjectId = subjectPair.Value;

                    var topics = GetTopics(subjectCode, gradeNumber);

                    foreach (var topic in topics)
                    {
                        lessons.Add(new DefaultLesson(
                            Guid.NewGuid(),
                            topic,
                            $"Core lesson about {topic}",
                            topic,
                            gradeId,
                            subjectId
                        ));
                    }
                }
            }

            context.AddRange(lessons);
        }

        private static string[] GetTopics(string subjectCode, int grade)
        {
            return subjectCode switch
            {
                "MATH" => grade switch
                {
                    <= 2 => new[]
                    {
                            "Counting Numbers",
                            "Addition and Subtraction",
                            "Basic Shapes",
                            "Measuring Length",
                            "Word Problems"
                        },

                    <= 5 => new[]
                    {
                            "Multiplication and Division",
                            "Fractions",
                            "Decimals",
                            "Perimeter and Area",
                            "Math Problem Solving"
                        },

                    <= 9 => new[]
                    {
                            "Algebra Basics",
                            "Linear Equations",
                            "Geometry Fundamentals",
                            "Statistics",
                            "Graph Interpretation"
                        },

                    _ => new[]
                    {
                            "Functions",
                            "Trigonometry",
                            "Calculus Introduction",
                            "Probability",
                            "Advanced Problem Solving"
                        }
                },

                "VIET" => grade switch
                {
                    <= 2 => new[]
                    {
                            "Alphabet and Pronunciation",
                            "Simple Sentences",
                            "Listening and Repeating",
                            "Reading Short Stories",
                            "Basic Writing"
                        },

                    <= 5 => new[]
                    {
                            "Reading Comprehension",
                            "Descriptive Writing",
                            "Grammar Basics",
                            "Storytelling",
                            "Vocabulary Building"
                        },

                    <= 9 => new[]
                    {
                            "Narrative Text",
                            "Poetry",
                            "Literary Analysis",
                            "Argumentative Writing",
                            "Vietnamese Grammar"
                        },

                    _ => new[]
                    {
                            "Classic Vietnamese Literature",
                            "Modern Literature",
                            "Essay Writing",
                            "Text Analysis",
                            "Critical Thinking in Literature"
                        }
                },

                "ENG" => new[]
                {
                        "Vocabulary",
                        "Grammar",
                        "Reading",
                        "Listening",
                        "Writing"
                    },

                "PHYS" => new[]
                {
                        "Motion",
                        "Force",
                        "Energy",
                        "Electricity",
                        "Practical Applications"
                    },

                "CHEM" => new[]
                {
                        "Atoms and Molecules",
                        "Chemical Reactions",
                        "Periodic Table",
                        "Chemical Calculations",
                        "Chemistry in Life"
                    },

                "BIO" => new[]
                {
                        "Cells",
                        "Human Body",
                        "Plants",
                        "Ecosystems",
                        "Genetics Basics"
                    },

                "HIS" => new[]
                {
                        "Ancient Vietnam",
                        "Feudal Dynasties",
                        "Colonial Period",
                        "Modern Vietnam",
                        "World History Connections"
                    },

                "GEO" => new[]
                {
                        "Maps and Geography Skills",
                        "Vietnam Geography",
                        "Climate",
                        "Population",
                        "Natural Resources"
                    },

                "SCI" => new[]
                {
                        "Scientific Observation",
                        "Matter",
                        "Energy",
                        "Earth Science",
                        "Environment"
                    },

                "TECH" => new[]
                {
                        "Basic Tools",
                        "Engineering Thinking",
                        "Simple Machines",
                        "Technology in Life",
                        "Design Project"
                    },

                "ART" => new[]
                {
                        "Drawing Basics",
                        "Color Theory",
                        "Craft",
                        "Art Appreciation",
                        "Creative Project"
                    },

                "PE" => new[]
                {
                        "Warm-up Exercises",
                        "Team Sports",
                        "Fitness Training",
                        "Coordination",
                        "Health Education"
                    },

                "CIT" => new[]
                {
                        "Community Rules",
                        "Responsibility",
                        "Ethics",
                        "Citizenship",
                        "Life Skills"
                    },

                _ => new[] { "Lesson 1", "Lesson 2", "Lesson 3", "Lesson 4", "Lesson 5" }
            };
        }
    }
}

