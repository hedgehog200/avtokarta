// Copyright (c) 2026 WebARTup - Studio: Technologies
// Все права защищены. Использование без лицензии запрещено.
// Лицензия: см. файл LICENSE в корне проекта.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AVTOKarta.Converters
{
    public class NavStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s)
            {
                if (s == "Active")
                {
                    var style = new System.Windows.Style(typeof(Button));

                    var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                    trigger.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x45, 0x5A, 0x65))));

                    style.Triggers.Add(trigger);
                    style.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x37, 0x47, 0x4F))));
                    style.Setters.Add(new Setter(Button.ForegroundProperty, System.Windows.Media.Brushes.White));
                    style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                    style.Setters.Add(new Setter(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
                    style.Setters.Add(new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
                    style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(14, 10, 14, 10)));
                    style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(4, 0, 4, 2)));
                    style.Setters.Add(new Setter(Button.FontSizeProperty, 13.0));
                    style.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));

                    var template = new System.Windows.Controls.ControlTemplate(typeof(Button));
                    var border = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
                    border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x37, 0x47, 0x4F)));
                    border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(4));
                    border.SetValue(System.Windows.Controls.Border.PaddingProperty, new Thickness(14, 10, 14, 10));
                    border.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(0));
                    var cp = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
                    cp.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                    cp.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                    border.AppendChild(cp);
                    template.VisualTree = border;
                    style.Setters.Add(new Setter(Button.TemplateProperty, template));

                    return style;
                }
            }

            var normalStyle = new System.Windows.Style(typeof(Button));
            normalStyle.Setters.Add(new Setter(Button.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            normalStyle.Setters.Add(new Setter(Button.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xBE, 0xC5))));
            normalStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            normalStyle.Setters.Add(new Setter(Button.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
            normalStyle.Setters.Add(new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Left));
            normalStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(14, 10, 14, 10)));
            normalStyle.Setters.Add(new Setter(Button.MarginProperty, new Thickness(4, 0, 4, 2)));
            normalStyle.Setters.Add(new Setter(Button.FontSizeProperty, 13.0));
            normalStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));

            var normalTemplate = new System.Windows.Controls.ControlTemplate(typeof(Button));
            var normalBorder = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            normalBorder.SetValue(System.Windows.Controls.Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            normalBorder.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(4));
            normalBorder.SetValue(System.Windows.Controls.Border.PaddingProperty, new Thickness(14, 10, 14, 10));
            normalBorder.SetValue(System.Windows.Controls.Border.MarginProperty, new Thickness(0));
            var normalHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            normalTemplate.Triggers.Add(normalHoverTrigger);
            var normalCp = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            normalCp.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            normalCp.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            normalBorder.AppendChild(normalCp);
            normalTemplate.VisualTree = normalBorder;

            var hoverSetter = new Setter(System.Windows.Controls.Border.BackgroundProperty,
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x37, 0x47, 0x4F)));
            normalHoverTrigger.Setters.Add(hoverSetter);

            normalStyle.Setters.Add(new Setter(Button.TemplateProperty, normalTemplate));
            return normalStyle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
