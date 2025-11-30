using System.Collections.Generic;
using System.Text;

namespace RALogicEditor.Models
{
    public class RichPresenceScript
    {
        public List<RichPresenceLookup> Lookups { get; set; } = new();
        public List<RichPresenceDisplayString> DisplayStrings { get; set; } = new();

        public override string ToString()
        {
            var sb = new StringBuilder();

            // Lookups
            foreach (var lookup in Lookups)
            {
                if (lookup.Entries.Count > 0 || lookup.Default != null)
                {
                    sb.AppendLine($"Lookup:{lookup.Name}");
                    foreach (var entry in lookup.Entries)
                        sb.AppendLine($"{entry.Key}={entry.Value}");
                    if (lookup.Default != null) sb.AppendLine($"*={lookup.Default}");
                    sb.AppendLine();
                }
            }

            // Formatters
            foreach (var fmt in Lookups)
            {
                if (fmt.Entries.Count == 0 && fmt.Default == null)
                {
                    sb.AppendLine($"Format:{fmt.Name}");
                    sb.AppendLine($"FormatType={fmt.Format}");
                    sb.AppendLine();
                }
            }

            // Display
            sb.AppendLine("Display:");
            foreach (var ds in DisplayStrings)
            {
                string line = "";
                if (!string.IsNullOrEmpty(ds.Condition)) line += $"?{ds.Condition}?";
                line += ds.Text;
                sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }

    public class RichPresenceLookup
    {
        public string Name { get; set; } = "";
        public string Format { get; set; } = "VALUE";
        public string? Default { get; set; }
        public List<LookupEntry> Entries { get; set; } = new();
    }

    public class LookupEntry
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public class RichPresenceDisplayString
    {
        public string Condition { get; set; } = "";
        public string Text { get; set; } = "";
    }
}