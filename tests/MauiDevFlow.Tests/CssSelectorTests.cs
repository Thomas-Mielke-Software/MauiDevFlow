using MauiDevFlow.Agent.Core;
using MauiDevFlow.Agent.Core.Css;

namespace MauiDevFlow.Tests;

public class CssSelectorTests
{
    // Helper to build a test tree:
    //   Window
    //   ├─ StackLayout
    //   │  ├─ Label (text="Hello World", automationId="greeting", styleClass=["primary"])
    //   │  ├─ Button (text="Save", automationId="saveBtn", visible, enabled)
    //   │  └─ Button (text="Cancel", automationId="cancelBtn", visible, disabled)
    //   └─ Grid
    //      ├─ Entry (text="", automationId="inputField", focused)
    //      └─ Label (text="Status", visible=false)
    static List<ElementInfo> BuildTestTree()
    {
        var label1 = new ElementInfo
        {
            Id = "label1", ParentId = "stack", Type = "Label", FullType = "Microsoft.Maui.Controls.Label",
            AutomationId = "greeting", Text = "Hello World", IsVisible = true, IsEnabled = true,
            StyleClass = new List<string> { "primary", "bold" }
        };
        var button1 = new ElementInfo
        {
            Id = "saveBtn", ParentId = "stack", Type = "Button", FullType = "Microsoft.Maui.Controls.Button",
            AutomationId = "saveBtn", Text = "Save", IsVisible = true, IsEnabled = true
        };
        var button2 = new ElementInfo
        {
            Id = "cancelBtn", ParentId = "stack", Type = "Button", FullType = "Microsoft.Maui.Controls.Button",
            AutomationId = "cancelBtn", Text = "Cancel", IsVisible = true, IsEnabled = false
        };
        var stack = new ElementInfo
        {
            Id = "stack", ParentId = "window", Type = "StackLayout", FullType = "Microsoft.Maui.Controls.StackLayout",
            IsVisible = true, IsEnabled = true,
            Children = new List<ElementInfo> { label1, button1, button2 }
        };
        var entry = new ElementInfo
        {
            Id = "entry1", ParentId = "grid", Type = "Entry", FullType = "Microsoft.Maui.Controls.Entry",
            AutomationId = "inputField", Text = "", IsVisible = true, IsEnabled = true, IsFocused = true
        };
        var hiddenLabel = new ElementInfo
        {
            Id = "statusLabel", ParentId = "grid", Type = "Label", FullType = "Microsoft.Maui.Controls.Label",
            Text = "Status", IsVisible = false, IsEnabled = true
        };
        var grid = new ElementInfo
        {
            Id = "grid", ParentId = "window", Type = "Grid", FullType = "Microsoft.Maui.Controls.Grid",
            IsVisible = true, IsEnabled = true,
            Children = new List<ElementInfo> { entry, hiddenLabel }
        };
        var window = new ElementInfo
        {
            Id = "window", Type = "Window", FullType = "Microsoft.Maui.Controls.Window",
            IsVisible = true, IsEnabled = true,
            Children = new List<ElementInfo> { stack, grid }
        };

        return new List<ElementInfo> { window };
    }

    [Fact]
    public void TypeSelector_MatchesByShortName()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Button");
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Button", r.Type));
    }

    [Fact]
    public void TypeSelector_CaseInsensitive()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "button");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void UniversalSelector_MatchesAll()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "*");
        // Verify key element types are present
        Assert.Contains(results, r => r.Type == "StackLayout");
        Assert.Contains(results, r => r.Type == "Grid");
        Assert.Contains(results, r => r.Type == "Label");
        Assert.Contains(results, r => r.Type == "Button");
        Assert.Contains(results, r => r.Type == "Entry");
        Assert.True(results.Count >= 7);
    }

    [Fact]
    public void IdSelector_MatchesByAutomationId()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "#saveBtn");
        Assert.Single(results);
        Assert.Equal("saveBtn", results[0].AutomationId);
    }

    [Fact]
    public void IdSelector_CaseInsensitive()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "#SAVEBTN");
        Assert.Single(results);
    }

    [Fact]
    public void ClassSelector_MatchesByStyleClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, ".primary");
        Assert.Single(results);
        Assert.Equal("greeting", results[0].AutomationId);
    }

    [Fact]
    public void ClassSelector_MultipleClasses()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, ".primary.bold");
        Assert.Single(results);
        Assert.Equal("greeting", results[0].AutomationId);
    }

    [Fact]
    public void AttributeExact_MatchesByText()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "[Text=\"Save\"]");
        Assert.Single(results);
        Assert.Equal("Save", results[0].Text);
    }

    [Fact]
    public void AttributePrefix_StartsWithMatch()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "[Text^=\"Hello\"]");
        Assert.Single(results);
        Assert.Equal("Hello World", results[0].Text);
    }

    [Fact]
    public void AttributeSuffix_EndsWithMatch()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "[Text$=\"World\"]");
        Assert.Single(results);
        Assert.Equal("Hello World", results[0].Text);
    }

    [Fact]
    public void AttributeSubstring_ContainsMatch()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "[Text*=\"lo Wo\"]");
        Assert.Single(results);
    }

    [Fact]
    public void AttributeExists_NonNullNonEmpty()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "[AutomationId]");
        // greeting, saveBtn, cancelBtn, inputField = 4
        Assert.Equal(4, results.Count);
    }

    [Fact]
    public void DescendantCombinator_FindsNestedElements()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout Button");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ChildCombinator_FindsDirectChildren()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Window > Button");
        Assert.Empty(results); // Buttons are not direct children of Window
    }

    [Fact]
    public void ChildCombinator_DirectChildrenOnly()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout > Button");
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void AdjacentSibling_ImmediateNext()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Label + Button");
        Assert.Single(results);
        Assert.Equal("Save", results[0].Text); // Button right after Label
    }

    [Fact]
    public void GeneralSibling_AllFollowing()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Label ~ Button");
        Assert.Equal(2, results.Count); // Both buttons follow the Label
    }

    [Fact]
    public void GroupSelector_CommaOR()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Entry, Button");
        Assert.Equal(3, results.Count); // 2 buttons + 1 entry
    }

    [Fact]
    public void FirstChild_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout > :first-child");
        Assert.Single(results);
        Assert.Equal("Label", results[0].Type);
        Assert.Equal("Hello World", results[0].Text);
    }

    [Fact]
    public void LastChild_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout > :last-child");
        Assert.Single(results);
        Assert.Equal("Cancel", results[0].Text);
    }

    [Fact]
    public void NthChild_PseudoClass()
    {
        var tree = BuildTestTree();
        // Note: Fizzler's nth-child parser has a limitation — nth-child(2)
        // generates NthChild(a=1, b=2) meaning "every child from position 2 onward",
        // not just "the 2nd child". This is a known Fizzler TODO.
        var results = CssSelectorEngine.Query(tree, "StackLayout > :nth-child(2)");
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Text == "Save");
        Assert.Contains(results, r => r.Text == "Cancel");
    }

    [Fact]
    public void OnlyChild_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, ":only-child");
        // No elements are only children in this tree
        Assert.Empty(results);
    }

    [Fact]
    public void Empty_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, ":empty");
        // Elements without children: label1, saveBtn, cancelBtn, entry, hiddenLabel = 5
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public void Negation_Not()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Button:not(#cancelBtn)");
        Assert.Single(results);
        Assert.Equal("Save", results[0].Text);
    }

    // --- MAUI pseudo-classes ---

    [Fact]
    public void Visible_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Label:visible");
        Assert.Single(results);
        Assert.Equal("Hello World", results[0].Text);
    }

    [Fact]
    public void Hidden_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Label:hidden");
        Assert.Single(results);
        Assert.Equal("Status", results[0].Text);
    }

    [Fact]
    public void Enabled_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Button:enabled");
        Assert.Single(results);
        Assert.Equal("Save", results[0].Text);
    }

    [Fact]
    public void Disabled_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Button:disabled");
        Assert.Single(results);
        Assert.Equal("Cancel", results[0].Text);
    }

    [Fact]
    public void Focused_PseudoClass()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, ":focused");
        Assert.Single(results);
        Assert.Equal("Entry", results[0].Type);
    }

    [Fact]
    public void NotVisible_Negation()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Label:not(:visible)");
        Assert.Single(results);
        Assert.Equal("Status", results[0].Text);
    }

    [Fact]
    public void CompoundSelector_TypeAndAttribute()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Button[Text=\"Save\"]");
        Assert.Single(results);
        Assert.Equal("saveBtn", results[0].AutomationId);
    }

    [Fact]
    public void ComplexSelector_DescendantWithAttribute()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "Grid Label:hidden");
        Assert.Single(results);
        Assert.Equal("Status", results[0].Text);
    }

    [Fact]
    public void ComplexSelector_ChildWithEnabled()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout > Button:enabled");
        Assert.Single(results);
        Assert.Equal("Save", results[0].Text);
    }

    [Fact]
    public void ResultsHaveNoChildren()
    {
        var tree = BuildTestTree();
        var results = CssSelectorEngine.Query(tree, "StackLayout");
        Assert.Single(results);
        Assert.Null(results[0].Children);
    }

    [Fact]
    public void InvalidSelector_ThrowsFormatException()
    {
        var tree = BuildTestTree();
        Assert.Throws<FormatException>(() => CssSelectorEngine.Query(tree, ">>>"));
    }
}

public class SelectorPreprocessorTests
{
    [Theory]
    [InlineData(":visible", "[__maui-visible]")]
    [InlineData(":hidden", "[__maui-hidden]")]
    [InlineData(":enabled", "[__maui-enabled]")]
    [InlineData(":disabled", "[__maui-disabled]")]
    [InlineData(":focused", "[__maui-focused]")]
    public void Transforms_MauiPseudoClasses(string input, string expected)
    {
        Assert.Equal(expected, SelectorPreprocessor.Preprocess(input));
    }

    [Fact]
    public void Preserves_StandardPseudoClasses()
    {
        Assert.Equal(":first-child", SelectorPreprocessor.Preprocess(":first-child"));
        Assert.Equal(":last-child", SelectorPreprocessor.Preprocess(":last-child"));
        Assert.Equal(":nth-child(2)", SelectorPreprocessor.Preprocess(":nth-child(2)"));
        Assert.Equal(":empty", SelectorPreprocessor.Preprocess(":empty"));
    }

    [Fact]
    public void Transforms_InCompoundSelectors()
    {
        Assert.Equal("Button[__maui-visible]", SelectorPreprocessor.Preprocess("Button:visible"));
    }

    [Fact]
    public void Transforms_InsideNot()
    {
        Assert.Equal(":not([__maui-visible])", SelectorPreprocessor.Preprocess(":not(:visible)"));
    }

    [Fact]
    public void Transforms_Multiple_InSelector()
    {
        Assert.Equal("Button[__maui-visible][__maui-enabled]",
            SelectorPreprocessor.Preprocess("Button:visible:enabled"));
    }

    [Fact]
    public void CaseInsensitive_Input()
    {
        Assert.Equal("[__maui-visible]", SelectorPreprocessor.Preprocess(":Visible"));
        Assert.Equal("[__maui-hidden]", SelectorPreprocessor.Preprocess(":HIDDEN"));
    }

    [Fact]
    public void PassesThrough_NoMauiPseudos()
    {
        var input = "Button.primary > Label[Text=\"Hello\"]";
        Assert.Equal(input, SelectorPreprocessor.Preprocess(input));
    }
}
