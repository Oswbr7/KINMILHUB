using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace GAMEHUB_V3
{
    public static class HighlightBehavior
    {
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.RegisterAttached(
                "SearchText",
                typeof(string),
                typeof(HighlightBehavior),
                new PropertyMetadata(string.Empty, OnSearchTextChanged));

        public static string GetSearchText(DependencyObject obj)
        {
            return (string)obj.GetValue(SearchTextProperty);
        }

        public static void SetSearchText(DependencyObject obj, string value)
        {
            obj.SetValue(SearchTextProperty, value);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock textBlock)
                return;

            string searchText = e.NewValue as string ?? string.Empty;
            string fullText = textBlock.Text ?? string.Empty;

            textBlock.Inlines.Clear();

            if (string.IsNullOrEmpty(searchText))
            {
                textBlock.Inlines.Add(new Run(fullText));
                return;
            }

            // Búsqueda insensible a mayúsculas/minúsculas
            var regex = new Regex(Regex.Escape(searchText), RegexOptions.IgnoreCase);
            int lastIndex = 0;

            foreach (Match match in regex.Matches(fullText))
            {
                // Parte antes del match
                if (match.Index > lastIndex)
                {
                    textBlock.Inlines.Add(new Run(fullText.Substring(lastIndex, match.Index - lastIndex))
                    {
                        Foreground = Brushes.White
                    });
                }

                // Parte resaltada
                textBlock.Inlines.Add(new Run(fullText.Substring(match.Index, match.Length))
                {
                    Foreground = Brushes.Orange,
                    FontWeight = FontWeights.Bold
                });

                lastIndex = match.Index + match.Length;
            }

            // Parte final
            if (lastIndex < fullText.Length)
            {
                textBlock.Inlines.Add(new Run(fullText.Substring(lastIndex))
                {
                    Foreground = Brushes.White
                });
            }
        }
    }
}
