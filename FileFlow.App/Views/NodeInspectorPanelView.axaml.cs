using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace FileFlow.App.Views;

public partial class NodeInspectorPanelView : UserControl
{
    public NodeInspectorPanelView()
    {
        InitializeComponent();

        AddHandler(InputElement.GotFocusEvent, (sender, e) =>
        {
            if (e.Source is Avalonia.Visual visual)
            {
                var acb = visual.FindAncestorOfType<AutoCompleteBox>() ?? (visual is AutoCompleteBox box ? box : null);
                if (acb != null && acb.MinimumPrefixLength == 0 && !acb.IsDropDownOpen)
                {
                    acb.IsDropDownOpen = true;
                }
            }
        });
    }
}
