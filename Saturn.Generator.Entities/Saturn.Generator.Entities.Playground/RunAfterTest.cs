using System.Diagnostics.CodeAnalysis;
using GoLive.Generator.Saturn.Resources;
using GoLive.Saturn.Data.Entities;

namespace Saturn.Generator.Entities.Playground;

[AddParentItemToLimitedView("*", "_shortId", ChildField = "Id", InheritFromIUniquelyIdentifiable = true)]
public partial class RunAfterTest : MultiscopedEntity<MainItem>
{
    /// <summary>
    /// This is an example XML Doc
    /// </summary>
    [AddToLimitedView("View1", true, UseLimitedView = "View1")]
    [AddRefToScope] 
    private Ref<MainItem> mainItemView1;

    /// <summary>
    /// This is a xml comment test
    /// </summary>
    private string test3;
    /*private void mainItemView1_runAfterSet(Ref<MainItem> incoming)
    {
        
    }
    
    private void mainItemView1_runAfterSet(MainItem incoming)
    {
        
    }  */  
    
    /*private void mainItemView1_runAfterSet(string incoming)
    {
        
    }*/

    /*private Func<string, MainItem> fetchMainItem;*/

    /*void test()
    {
        fetchMainItem = s => 
    }*/

    public MainItem Test { get; set; }
}