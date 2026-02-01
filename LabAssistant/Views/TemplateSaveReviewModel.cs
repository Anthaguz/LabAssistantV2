namespace LabAssistant.Views
{
    public class TemplateSaveReviewModel
    {
        public TemplateSaveReviewModel(string name, string id, int vmCount)
        {
            Name = name;
            Id = id;
            VmCount = vmCount;
        }

        public string Name { get; }
        public string Id { get; }
        public int VmCount { get; }
    }
}
