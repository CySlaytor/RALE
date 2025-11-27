using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Services.Explainer; // Added missing reference
using System.Collections.Generic;
using System.Text;

namespace RALogicEditor.Services.Explainer.Processors
{
    // Handles AndNext / OrNext / ResetNextIf chains
    public class AndNextProcessor : ILogicProcessor
    {
        public int Priority => 15;

        public bool CanProcess(List<AchievementCondition> conditions, int index)
        {
            string f = conditions[index].Flag;
            return f == "AndNext" || f == "OrNext" || f == "ResetNextIf";
        }

        public ProcessorResult Process(ExplanationContext ctx, List<AchievementCondition> conditions, int index)
        {
            var chainItems = new List<(string Text, string Connector)>();
            var resetConditions = new List<string>();
            int ptr = index;

            // 1. Consume the chain of Logic Flags
            while (ptr < conditions.Count)
            {
                var cond = conditions[ptr];
                string f = cond.Flag;

                if (f != "AndNext" && f != "OrNext" && f != "ResetNextIf") break;

                // Process the gate condition
                var std = new StandardConditionProcessor().Process(ctx, conditions, ptr);

                if (f == "ResetNextIf")
                {
                    resetConditions.Add(std.Text);
                }
                else
                {
                    string connector = (f == "OrNext") ? "OR" : "AND";
                    chainItems.Add((std.Text, connector));
                }

                ptr++;
            }

            // 2. Skip over Add Address lines to find the Target Condition
            while (ptr < conditions.Count && conditions[ptr].Flag == "Add Address")
            {
                ptr++;
            }

            string resultText = "";
            int consumed = ptr - index;
            var category = ExplanationCategory.Context;

            if (ptr < conditions.Count)
            {
                // 3. Resolve Target Text
                // We delegate the processing of the target to the full processor list (manually checking priority)
                // to ensure that if the target is a Transition, Accumulator, Pause, or Reset, it gets formatted correctly.

                ProcessorResult targetRes;
                var transProc = new TransitionProcessor();
                var measuredProc = new MeasuredTriggerProcessor();
                var resetProc = new ResetProcessor();
                var pauseProc = new PauseProcessor();
                var standardProc = new StandardConditionProcessor();

                // Priority Check: Transition -> Measured -> Reset -> Pause -> Standard
                if (transProc.CanProcess(conditions, ptr))
                {
                    targetRes = transProc.Process(ctx, conditions, ptr);
                }
                else if (measuredProc.CanProcess(conditions, ptr))
                {
                    targetRes = measuredProc.Process(ctx, conditions, ptr);
                }
                else if (resetProc.CanProcess(conditions, ptr))
                {
                    targetRes = resetProc.Process(ctx, conditions, ptr);
                }
                else if (pauseProc.CanProcess(conditions, ptr))
                {
                    targetRes = pauseProc.Process(ctx, conditions, ptr);
                }
                else
                {
                    targetRes = standardProc.Process(ctx, conditions, ptr);
                }

                consumed += targetRes.ConditionsConsumed; // Add the target's consumption count
                category = targetRes.Category; // Adopt the category of the target (e.g. Transition, Reset, Pause)

                // 4. Build Final Logic String
                // Format: "(ChainItem1 [AND/OR] ChainItem2 [AND/OR] Target)"
                var sb = new StringBuilder();

                if (chainItems.Count > 0)
                {
                    // Unwrap the target text if it is a complex sentence (like MeasuredIf, Reset, Pause)
                    // so we can place the logic chain inside the condition, not outside the sentence.
                    string targetText = targetRes.Text;
                    string prefixWrapper = "";
                    string suffixWrapper = "";

                    if (category == ExplanationCategory.MeasuredIf)
                    {
                        string p = "The Measured condition will work if ";
                        string s = " (MeasuredIf)";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p;
                            suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Reset)
                    {
                        string p = "If ";
                        string s = ", reset the achievement.";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p;
                            suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Pause)
                    {
                        string p = "If ";
                        // Pause suffix varies slightly
                        if (targetText.StartsWith(p))
                        {
                            int lastComma = targetText.LastIndexOf(',');
                            if (lastComma > p.Length)
                            {
                                prefixWrapper = p;
                                suffixWrapper = targetText.Substring(lastComma); // includes .
                                targetText = targetText.Substring(p.Length, lastComma - p.Length);
                            }
                        }
                    }

                    sb.Append(prefixWrapper);
                    sb.Append("(");
                    foreach (var item in chainItems)
                    {
                        sb.Append(item.Text);
                        sb.Append($" {item.Connector} ");
                    }
                    sb.Append(targetText);
                    sb.Append(")");
                    sb.Append(suffixWrapper);
                }
                else
                {
                    // Only ResetNextIf existed in the chain, just show target
                    sb.Append(targetRes.Text);
                }

                if (resetConditions.Count > 0)
                {
                    string resets = string.Join(" OR ", resetConditions);
                    sb.Append($" (resets if {resets})");
                }

                resultText = sb.ToString();
            }
            else
            {
                // Incomplete chain
                var sb = new StringBuilder();
                foreach (var item in chainItems) sb.Append($"{item.Text} {item.Connector} ");
                resultText = $"{sb} (Incomplete Chain)";
            }

            return new ProcessorResult
            {
                Text = resultText,
                Category = category,
                ConditionsConsumed = consumed
            };
        }
    }
}