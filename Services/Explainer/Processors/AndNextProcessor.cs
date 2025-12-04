using RALogicEditor.Definitions;
using RALogicEditor.Models;
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

                // Skip Add Address lines so they don't break the chain detection
                if (f == "Add Address")
                {
                    ptr++;
                    continue;
                }

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
                // DELEGATION: We must check ALL high-level processors to handle complex targets
                ProcessorResult targetRes;

                // Instantiate processors (in order of priority)
                var transProc = new TransitionProcessor();
                var asciiProc = new AsciiStringProcessor(); // Added
                var accumProc = new AccumulatorProcessor(); // <--- CRITICAL ADDITION
                var measuredProc = new MeasuredTriggerProcessor();
                var resetProc = new ResetProcessor();
                var pauseProc = new PauseProcessor();
                var mathProc = new MathChainProcessor();
                var standardProc = new StandardConditionProcessor();

                // Priority Check: Ensure complex logic is caught before standard fallbacks
                if (transProc.CanProcess(conditions, ptr)) targetRes = transProc.Process(ctx, conditions, ptr);
                else if (asciiProc.CanProcess(conditions, ptr)) targetRes = asciiProc.Process(ctx, conditions, ptr);
                else if (accumProc.CanProcess(conditions, ptr)) targetRes = accumProc.Process(ctx, conditions, ptr); // <--- DELEGATE TO ACCUMULATOR
                else if (measuredProc.CanProcess(conditions, ptr)) targetRes = measuredProc.Process(ctx, conditions, ptr);
                else if (resetProc.CanProcess(conditions, ptr)) targetRes = resetProc.Process(ctx, conditions, ptr);
                else if (pauseProc.CanProcess(conditions, ptr)) targetRes = pauseProc.Process(ctx, conditions, ptr);
                else if (mathProc.CanProcess(conditions, ptr)) targetRes = mathProc.Process(ctx, conditions, ptr);
                else targetRes = standardProc.Process(ctx, conditions, ptr);

                consumed += targetRes.ConditionsConsumed;
                category = targetRes.Category;

                // 4. Build Final Logic String
                var sb = new StringBuilder();

                if (chainItems.Count > 0)
                {
                    string targetText = targetRes.Text;
                    string prefixWrapper = "";
                    string suffixWrapper = "";

                    // Unwrap complex sentences (e.g., "The Measured condition will work if...") 
                    // so we can place the AND/OR logic inside the sentence structure.
                    if (category == ExplanationCategory.MeasuredIf)
                    {
                        string p = "The Measured condition will work if ";
                        string s = " (MeasuredIf)";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p; suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Reset)
                    {
                        string p = "If "; string s = ", reset the achievement.";
                        if (targetText.StartsWith(p) && targetText.EndsWith(s))
                        {
                            prefixWrapper = p; suffixWrapper = s;
                            targetText = targetText.Substring(p.Length, targetText.Length - p.Length - s.Length);
                        }
                    }
                    else if (category == ExplanationCategory.Pause)
                    {
                        string p = "If ";
                        if (targetText.StartsWith(p))
                        {
                            int lastComma = targetText.LastIndexOf(',');
                            if (lastComma > p.Length)
                            {
                                prefixWrapper = p; suffixWrapper = targetText.Substring(lastComma);
                                targetText = targetText.Substring(p.Length, lastComma - p.Length);
                            }
                        }
                    }
                    else if (targetText.StartsWith("Calculate: "))
                    {
                        targetText = targetText.Substring(11);
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