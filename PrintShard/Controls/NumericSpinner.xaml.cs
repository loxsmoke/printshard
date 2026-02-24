using System.Windows;
using System.Windows.Controls;

namespace PrintShard.Controls;

/// <summary>
/// A numeric spinner control with up/down buttons for incrementing/decrementing a double value.
/// </summary>
public partial class NumericSpinner : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(
            nameof(Value),
            typeof(double),
            typeof(NumericSpinner),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(
            nameof(Minimum),
            typeof(double),
            typeof(NumericSpinner),
            new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(
            nameof(Maximum),
            typeof(double),
            typeof(NumericSpinner),
            new PropertyMetadata(100.0, OnRangeChanged));

    public static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register(
            nameof(Increment),
            typeof(double),
            typeof(NumericSpinner),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty StringFormatProperty =
        DependencyProperty.Register(
            nameof(StringFormat),
            typeof(string),
            typeof(NumericSpinner),
            new PropertyMetadata("F1", OnStringFormatChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Increment
    {
        get => (double)GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public NumericSpinner()
    {
        InitializeComponent();
        UpdateButtonStates();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumericSpinner spinner)
            spinner.UpdateButtonStates();
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumericSpinner spinner)
            spinner.UpdateButtonStates();
    }

    private static void OnStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NumericSpinner spinner)
            spinner.UpdateTextBinding();
    }

    private void UpdateTextBinding()
    {
        var binding = new System.Windows.Data.Binding(nameof(Value))
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(NumericSpinner), 1),
            UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.LostFocus,
            StringFormat = StringFormat
        };
        ValueTextBox.SetBinding(System.Windows.Controls.TextBox.TextProperty, binding);
    }

    private void UpButton_Click(object sender, RoutedEventArgs e)
    {
        var newValue = Math.Min(Value + Increment, Maximum);
        Value = Math.Round(newValue, 1);
    }

    private void DownButton_Click(object sender, RoutedEventArgs e)
    {
        var newValue = Math.Max(Value - Increment, Minimum);
        Value = Math.Round(newValue, 1);
    }

    private void UpdateButtonStates()
    {
        if (DownButton != null)
            DownButton.IsEnabled = Value > Minimum;
        if (UpButton != null)
            UpButton.IsEnabled = Value < Maximum;
    }
}
