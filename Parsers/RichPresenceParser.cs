using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RALogicEditor.Parsers
{
    public static class RichPresenceParser
    {
        public static RichPresenceScript Parse(string content)
        {
            var script = new RichPresenceScript();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string section = "";
            RichPresenceLookup? currentLookup = null;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("//")) continue;

                if (line.StartsWith("Lookup:"))
                {
                    section = "Lookup";
                    currentLookup = new RichPresenceLookup { Name = line.Substring(7).Trim() };
                    script.Lookups.Add(currentLookup);
                    continue;
                }
                if (line.StartsWith("Format:"))
                {
                    section = "Format";
                    currentLookup = new RichPresenceLookup { Name = line.Substring(7).Trim() };
                    script.Lookups.Add(currentLookup);
                    continue;
                }
                if (line.StartsWith("Display:"))
                {
                    section = "Display";
                    continue;
                }

                if (section == "Lookup" && currentLookup != null)
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        if (parts[0].Trim() == "*") currentLookup.Default = parts[1].Trim();
                        else currentLookup.Entries.Add(new LookupEntry { Key = parts[0].Trim(), Value = parts[1].Trim() });
                    }
                }
                else if (section == "Format" && currentLookup != null)
                {
                    if (line.StartsWith("FormatType=")) currentLookup.Format = line.Substring(11).Trim();
                }
                else if (section == "Display")
                {
                    var ds = new RichPresenceDisplayString();
                    string temp = line;
                    if (temp.StartsWith("?"))
                    {
                        int endQ = temp.IndexOf('?', 1);
                        if (endQ > 1)
                        {
                            ds.Condition = temp.Substring(1, endQ - 1);
                            temp = temp.Substring(endQ + 1);
                        }
                    }
                    ds.Text = temp;
                    script.DisplayStrings.Add(ds);
                }
            }
            return script;
        }
    }
}