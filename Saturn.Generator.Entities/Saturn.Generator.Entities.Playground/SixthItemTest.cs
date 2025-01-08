using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;


public partial class SixthItemTest  : Entity
{
    [Required]
    public partial string WibbleWobble { get; set; }
}