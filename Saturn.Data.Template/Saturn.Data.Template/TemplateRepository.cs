using System.Data;

namespace Saturn.Data.Template;

public partial class TemplateRepository(IDbConnection connection)
{
    public void Dispose()
    {
    }
}

public class TemplateRepositoryOptions
{
    
}
