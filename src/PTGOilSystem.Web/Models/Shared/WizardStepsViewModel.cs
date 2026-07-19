namespace PTGOilSystem.Web.Models.Shared;

public sealed record WizardStepItem(int Number, string Label, bool IsActive = false, bool IsInteractive = true);

public sealed record WizardStepsViewModel(
    string AriaLabel,
    string StepDataAttribute,
    IReadOnlyList<WizardStepItem> Steps,
    string? CssClass = null);
