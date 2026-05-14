using Domain.CourseManagement.Enum;

namespace Domain.CourseManagement.Entity
{
    public class Material
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid MaterialID { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Url { get; private set; }
        public MaterialType Type { get; private set; }

        public Guid LessonID { get; private set; }
        #endregion

        protected Material() { }

        public Material(
            Guid materialId, 
            string name,
            string description,
            string url, 
            MaterialType type, 
            Guid lessonId)
        {
            MaterialID = materialId;
            Name = name;
            Description = description;
            Url = url;
            Type = type;
            LessonID = lessonId;
        }

        #region Methods
        #endregion
    }
}

