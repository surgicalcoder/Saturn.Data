using GoLive.Saturn.Data.Entities;
using GoLive.Saturn.Generator.Entities.Resources;

namespace Saturn.Generator.Entities.Playground;

[GenerateDto]
public partial class DtoTest : Entity
{
    public partial string? Name { get; set; }

    public partial int Count { get; set; }

    [AddToLimitedView("View1")]
    private Ref<MainItem> mainItem;
}
