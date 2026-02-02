namespace LabAssistant.Views
{
    public class TemplateSaveReviewModel
    {
        public TemplateSaveReviewModel(string name, string id, int vmCount, string? description, string version)
        {
            Name = name;
            Id = id;
            VmCount = vmCount;
            Description = description;
            Version = version;
        }

        public string Name { get; }
        public string Id { get; }
        public int VmCount { get; }
        public string? Description { get; }
        public string Version { get; }
    }
}
