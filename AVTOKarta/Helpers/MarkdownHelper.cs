// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace AVTOKarta.Helpers
{
    public static class MarkdownHelper
    {
        private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", RegexOptions.Compiled);

        public static FlowDocument ConvertToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x73, 0x73, 0x73))
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            string[] lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                string trimmed = line.TrimEnd('\r');

                if (trimmed.StartsWith("## "))
                {
                    var para = new Paragraph(new Bold(new Run(trimmed.Substring(3)))
                    {
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
                    });
                    para.Margin = new Thickness(0, 10, 0, 4);
                    doc.Blocks.Add(para);
                }
                else if (trimmed.StartsWith("### "))
                {
                    var para = new Paragraph(new Bold(new Run(trimmed.Substring(4)))
                    {
                        FontSize = 12.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33))
                    });
                    para.Margin = new Thickness(0, 8, 0, 3);
                    doc.Blocks.Add(para);
                }
                else if (trimmed.StartsWith("- "))
                {
                    string content = trimmed.Substring(2);
                    var para = new Paragraph();
                    para.Margin = new Thickness(10, 1, 0, 1);
                    para.Inlines.Add(new Run("  \u2022  "));
                    AddFormattedInlines(para, content);
                    doc.Blocks.Add(para);
                }
                else if (trimmed.StartsWith("---") || trimmed.StartsWith("***"))
                {
                    var para = new Paragraph { Margin = new Thickness(0, 6, 0, 6) };
                    var border = new System.Windows.Controls.Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)),
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    para.Inlines.Add(new InlineUIContainer(border));
                    doc.Blocks.Add(para);
                }
                else if (string.IsNullOrWhiteSpace(trimmed))
                {
                    var para = new Paragraph { Margin = new Thickness(0, 4, 0, 4) };
                    doc.Blocks.Add(para);
                }
                else
                {
                    var para = new Paragraph();
                    para.Margin = new Thickness(0, 2, 0, 2);
                    AddFormattedInlines(para, trimmed);
                    doc.Blocks.Add(para);
                }
            }

            return doc;
        }

        private static void AddFormattedInlines(Paragraph para, string text)
        {
            string pattern = @"(\*\*(.+?)\*\*)|(\*(.+?)\*)";
            int lastIndex = 0;

            foreach (Match match in Regex.Matches(text, pattern))
            {
                if (match.Index > lastIndex)
                    para.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));

                if (match.Groups[1].Success)
                    para.Inlines.Add(new Bold(new Run(match.Groups[2].Value)));
                else if (match.Groups[3].Success)
                    para.Inlines.Add(new Italic(new Run(match.Groups[4].Value)));

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
                para.Inlines.Add(new Run(text.Substring(lastIndex)));
        }
    }
}
