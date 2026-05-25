namespace VoiceLite.Models
{
    // User-editable replacement entry for the Pro custom dictionary.
    // Applied after the universal dev-term dictionary in TextPostProcessor.
    public class CustomDictionaryEntry
    {
        public string Spoken { get; set; } = string.Empty;
        public string Written { get; set; } = string.Empty;
    }
}
