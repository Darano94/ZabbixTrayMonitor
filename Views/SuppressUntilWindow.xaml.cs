using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ZabbixTrayMonitor.Views
{
    public partial class SuppressUntilWindow : Window
    {
        private static readonly Regex PartialTimePattern = new(
            @"^\d{0,2}(:\d{0,2})?$",
            RegexOptions.Compiled);

        public DateTime? SelectedDateTime { get; private set; }

        public SuppressUntilWindow()
        {
            InitializeComponent();

            var defaultTime = DateTime.Now.AddHours(1);
            DatePicker.SelectedDate = defaultTime.Date;
            TimeTextBox.Text = defaultTime.ToString("HH:mm");

            // PreviewTextInput greift nicht bei Einfügen per Zwischenablage.
            // Deshalb Paste zusätzlich prüfen, damit auch darüber kein Müll ins Feld kommt.
            DataObject.AddPastingHandler(TimeTextBox, TimeTextBox_Pasting);

            Loaded += (_, _) =>
            {
                TimeTextBox.Focus();
                TimeTextBox.SelectAll();
            };
        }

        private void Suppress_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDate(out var date))
            {
                ShowValidationError($"Bitte ein gültiges Datum im Format {CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern} eingeben.");
                DatePicker.Focus();
                return;
            }

            if (!TryGetTime(out var time))
            {
                ShowValidationError("Bitte eine gültige Uhrzeit im Format HH:mm eingeben, z. B. 22:30.");
                TimeTextBox.Focus();
                TimeTextBox.SelectAll();
                return;
            }

            var selected = new DateTime(
                date.Year,
                date.Month,
                date.Day,
                time.Hour,
                time.Minute,
                0,
                DateTimeKind.Local
            );

            if (selected <= DateTime.Now)
            {
                ShowValidationError("Der Zeitpunkt muss in der Zukunft liegen.");
                return;
            }

            SelectedDateTime = selected;
            DialogResult = true;
            Close();
        }

        private bool TryGetDate(out DateTime date)
        {
            var text = DatePicker.Text?.Trim() ?? string.Empty;

            // Nicht nur SelectedDate verwenden: Wenn jemand in das editierbare
            // DatePicker-Feld ungültigen Text tippt, könnte sonst noch das vorherige
            // gültige SelectedDate verwendet werden.
            if (!DateTime.TryParseExact(
                    text,
                    "d",
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.None,
                    out date))
            {
                date = default;
                return false;
            }

            date = date.Date;
            return true;
        }

        private bool TryGetTime(out DateTime time)
        {
            return DateTime.TryParseExact(
                TimeTextBox.Text.Trim(),
                "HH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out time);
        }

        private void TimeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            var proposedText = BuildProposedText(textBox, e.Text);
            e.Handled = !IsValidPartialTime(proposedText);
        }

        private void TimeTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox ||
                !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true))
            {
                e.CancelCommand();
                return;
            }

            var pastedText = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
            var proposedText = BuildProposedText(textBox, pastedText);

            if (!IsValidPartialTime(proposedText))
                e.CancelCommand();
        }

        private static string BuildProposedText(TextBox textBox, string insertedText)
        {
            var currentText = textBox.Text ?? string.Empty;
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;

            var withoutSelection = currentText.Remove(selectionStart, selectionLength);
            return withoutSelection.Insert(selectionStart, insertedText);
        }

        private static bool IsValidPartialTime(string text)
        {
            if (text.Length > 5 || !PartialTimePattern.IsMatch(text))
                return false;

            // Sobald beide Stundenstellen vorhanden sind, 00 bis 23 erzwingen.
            var colonIndex = text.IndexOf(':');
            var hourPart = colonIndex >= 0 ? text[..colonIndex] : text;

            if (hourPart.Length == 2 &&
                (!int.TryParse(hourPart, out var hour) || hour > 23))
            {
                return false;
            }

            // Sobald beide Minutenstellen vorhanden sind, 00 bis 59 erzwingen.
            if (colonIndex >= 0)
            {
                var minutePart = text[(colonIndex + 1)..];

                if (minutePart.Length == 2 &&
                    (!int.TryParse(minutePart, out var minute) || minute > 59))
                {
                    return false;
                }
            }

            return true;
        }

        private void DatePicker_DateValidationError(object? sender, DatePickerDateValidationErrorEventArgs e)
        {
            // WPF soll bei ungültiger manueller Eingabe keine Exception werfen.
            // Die eigentliche verständliche Fehlermeldung kommt beim Klick auf Unterdrücken.
            e.ThrowException = false;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            }
            catch { }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            DialogResult = false;
            Close();
        }

        private void ShowValidationError(string message)
        {
            MessageBox.Show(
                this,
                message,
                "Unterdrücken bis",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }
}
