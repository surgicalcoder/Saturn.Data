using GoLive.Saturn.Generator.Entities.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

public partial class ComputedExpressionItem : Entity
{
    public partial string Site { get; set; }

    [AddToLimitedView("Editable")]
    [AddToLimitedView("List", ComputedExpression = "{value} != null", LimitedViewType = typeof(bool))]
    public partial string SiteNotNull { get; set; }

    [AddToLimitedView("List", ComputedExpression = "{value}?.Length ?? 0", ComputedSelectorExpression = "{value} != null ? {value}.Length : 0", LimitedViewType = typeof(int))]
    public partial string SiteLength { get; set; }

    [AddToLimitedView("List", ComputedExpression = "{value} != null ? {value}.Length : default", LimitedViewType = typeof(int?))]
    public partial string SiteTernary { get; set; }
}