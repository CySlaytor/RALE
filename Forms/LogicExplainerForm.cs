using RALogicEditor.Models;
using RALogicEditor.Services; // Ensures LogicExplainer is visible
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;

namespace RALogicEditor.Forms
{
    public partial class LogicExplainerForm : Form
    {
        private IAssetItem? _asset;
        private List<AchievementConditionGroup> _groups = new();
        private Dictionary<string, List<NoteNode>> _notesLookup = new();
        private List<(long Start, long End, NoteNode Node)> _rangeCache = new();

        public LogicExplainerForm()
        {
            InitializeComponent();

            this.BackColor = Color.FromArgb(30, 30, 30);
            panelTop.BackColor = Color.FromArgb(30, 30, 30);
            chkShowDecimal.ForeColor = Color.Gainsboro;

            // Allow WebBrowser to handle shortcuts like Ctrl+C naturally
            webBrowser.WebBrowserShortcutsEnabled = true;
            webBrowser.IsWebBrowserContextMenuEnabled = false; // Cleaner UI

            chkShowDecimal.CheckedChanged += (s, e) => Render();
        }

        public void DisplayLogic(
            IAssetItem asset,
            List<AchievementConditionGroup> groups,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            _asset = asset;
            _groups = groups;
            _notesLookup = notesLookup;
            _rangeCache = rangeCache;

            this.Text = $"Logic Explainer - {asset.Title}";
            Render();
        }

        private void Render()
        {
            if (_asset == null) return;

            var result = LogicExplainer.ExplainAsset(
                _asset,
                _groups,
                _notesLookup,
                _rangeCache,
                chkShowDecimal.Checked
            );

            string html = BuildHtml(result);
            webBrowser.DocumentText = html;
        }

        private string BuildHtml(ExplanationResult result)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head>");
            // Force IE Edge mode for better rendering in standard WinForms WebBrowser
            sb.Append("<meta http-equiv='X-UA-Compatible' content='IE=edge' />");
            sb.Append("<style>");
            sb.Append("body { background-color: #1e1e1e; color: Gainsboro; font-family: 'Segoe UI', sans-serif; font-size: 10pt; margin: 10px; }");
            sb.Append("h1 { color: LightSkyBlue; font-size: 14pt; margin-bottom: 5px; }");
            sb.Append("h2 { color: White; font-size: 11pt; margin-top: 15px; margin-bottom: 5px; border-bottom: 1px solid #444; padding-bottom: 2px; }");
            sb.Append("h3 { color: LightGray; font-size: 10pt; text-decoration: underline; margin-top: 10px; margin-bottom: 2px; }");
            sb.Append(".line { margin-bottom: 2px; }");
            sb.Append(".reset { color: Yellow; }");
            sb.Append(".pause { color: #ff5050; }");
            sb.Append(".link { color: CornflowerBlue; cursor: pointer; text-decoration: none; }");
            sb.Append(".link:hover { text-decoration: underline; }");
            // Updated CSS: white-space: pre-wrap preserves indentation for recursive details
            sb.Append(".hidden-box { display: none; margin-left: 20px; margin-top: 5px; padding: 5px; border-left: 2px solid #444; background-color: #252526; color: #a0a0a0; white-space: pre-wrap; }");
            sb.Append(".code-line { font-family: Consolas, monospace; font-size: 9pt; }");
            sb.Append("</style>");
            sb.Append("<script>");
            sb.Append("function toggle(id, link) {");
            sb.Append("  var x = document.getElementById(id);");
            sb.Append("  if (x.style.display === 'block') {");
            sb.Append("    x.style.display = 'none';");
            sb.Append("    link.innerHTML = link.getAttribute('data-show-text');");
            sb.Append("  } else {");
            sb.Append("    x.style.display = 'block';");
            sb.Append("    link.innerHTML = '[Hide Details]';");
            sb.Append("  }");
            sb.Append("}");
            sb.Append("</script>");
            sb.Append("</head><body>");

            // Process the main text using recursive parser
            sb.Append(ParseContentRecursive(result.MainText, result));

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string ParseContentRecursive(string text, ExplanationResult result)
        {
            var sb = new StringBuilder();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string htmlLine = line;

                // 1. Helper to process inline tokens (EXPAND)
                // We refactor this into a function so we can call it inside headers too.
                string ProcessLineContent(string rawText)
                {
                    // Expansion Tokens [[EXPAND:ID:Count/Label]]
                    var match = Regex.Match(rawText, @"\[\[EXPAND:([a-f0-9]+):(.*?)\]\]");
                    if (match.Success)
                    {
                        string fullToken = match.Value;
                        string id = match.Groups[1].Value;
                        string labelArg = match.Groups[2].Value;

                        string linkText = labelArg.All(char.IsDigit)
                            ? $"[Show {labelArg} conditions]"
                            : $"[{labelArg}]";

                        // Get content from result details
                        string content = "";
                        if (result.DetailedInfo.ContainsKey(id))
                        {
                            // RECURSIVE CALL: Process the inner content for nested tokens
                            content = ParseContentRecursive(result.DetailedInfo[id], result);
                        }

                        string interactiveHtml = $"<span class='link' onclick='toggle(\"{id}\", this)' data-show-text='{linkText}'>{linkText}</span>";
                        interactiveHtml += $"<div id='{id}' class='hidden-box code-line'>{content}</div>";

                        // Replace the token in the line with the HTML widget
                        string pre = HtmlEncode(rawText.Substring(0, match.Index));

                        // FIX: Recursively process the remainder of the line to find subsequent expansion tokens
                        string remainder = rawText.Substring(match.Index + fullToken.Length);
                        string post = ProcessLineContent(remainder);

                        return $"{pre} {interactiveHtml} {post}";
                    }
                    return HtmlEncode(rawText);
                }

                // 2. Headers Logic (Now supports tokens)
                if (htmlLine.StartsWith("# "))
                {
                    string content = ProcessLineContent(htmlLine.Substring(2));
                    htmlLine = $"<h1>{content}</h1>";
                }
                else if (htmlLine.StartsWith("## "))
                {
                    string content = ProcessLineContent(htmlLine.Substring(3));
                    htmlLine = $"<h2>{content}</h2>";
                }
                else if (htmlLine.StartsWith("### "))
                {
                    string content = ProcessLineContent(htmlLine.Substring(4));
                    htmlLine = $"<h3>{content}</h3>";
                }
                else
                {
                    // 3. Standard Line Logic (Supports tokens)
                    // Check if the line itself contains a token first, before encoding
                    var match = Regex.Match(htmlLine, @"\[\[EXPAND:([a-f0-9]+):(.*?)\]\]");
                    if (match.Success)
                    {
                        // Reuse the helper logic, but wrap in div
                        htmlLine = $"<div class='line'>{ProcessLineContent(htmlLine)}</div>";
                    }
                    else
                    {
                        // Standard Line coloring
                        string cssClass = "";
                        if (htmlLine.Contains("reset the achievement")) cssClass = "reset";
                        else if (htmlLine.Contains("pause the achievement") || htmlLine.Contains("lock the achievement")) cssClass = "pause";

                        string safeText = HtmlEncode(htmlLine);
                        if (cssClass != "")
                            htmlLine = $"<div class='line {cssClass}'>{safeText}</div>";
                        else
                            htmlLine = $"<div class='line'>{safeText}</div>";
                    }
                }

                sb.Append(htmlLine);
            }
            return sb.ToString();
        }

        private string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return System.Net.WebUtility.HtmlEncode(text);
        }
    }
}