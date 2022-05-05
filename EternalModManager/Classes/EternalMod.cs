namespace EternalModManager.Classes
{
    // EternalMod JSON deserialization class
    public class EternalMod
    {
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public string? Version { get; set; }
        public int? LoadPriority { get; set; }
        public int? RequiredVersion { get; set; }
    }
}
