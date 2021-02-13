using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GoLive.Saturn.Data;
using GoLive.Saturn.Data.Abstractions;
using GoLive.Saturn.Data.Entities;

namespace GoLive.Saturn.InternalTests
{
    class ScopedTests
    {
        public async Task Run()
        {
            Repository repository = new Repository(new RepositoryOptions() { ConnectionString = "mongodb://localhost/GoLiveSaturn" });
            IScopedReadonlyRepository scoped = (IScopedReadonlyRepository) repository;

            var parent1 = new ParentScope() {Name = "Parent 1"};
            var parent2 = new ParentScope() {Name = "Parent 2"};
            var parent3 = new ParentScope() {Name = "Parent 3"};

            await repository.Add(parent1);
            await repository.Add(parent2);
            await repository.Add(parent3);


            var childItem1 = new ChildScope() {Name = "Child Item 1", Scope = parent1};
            var childItem2 = new ChildScope() {Name = "Child Item 2", Scope = parent1};
            var childItem3 = new ChildScope() {Name = "Child Item 3", Scope = parent2};
            var childItem4 = new ChildScope() {Name = "Child Item 4", Scope = parent2};
            var childItem5 = new ChildScope() {Name = "Child Item 5", Scope = parent3};
            var childItem6 = new ChildScope() {Name = "Child Item 6", Scope = parent3};
            var list1 = new List<ChildScope>(){childItem1, childItem2, childItem3, childItem4, childItem5, childItem6 };
            await repository.AddMany(list1);

            var item = (await scoped.ById<ChildScope, ParentScope>(parent1, childItem1.Id));
            var itm = (await scoped.ById<ChildScope, ParentScope>(parent1, childItem3.Id));
            var returnItems = (await scoped.All<ChildScope, ParentScope>(parent1)).ToList();
            var manyTest = (await scoped.Many<ChildScope, ParentScope>(parent2, c => c.Name.Contains("Child"))).ToList();
            var defaultCount = await repository.CountMany<ChildScope>(e => e.Name.Contains("Child"));
            var countInScope = (await scoped.CountMany<ChildScope, ParentScope>(parent3, e => e.Name.Contains("Child")));
        }
    }

    public class ParentScope : Entity
    {
        public string Name { get; set; }
    }

    public class ChildScope : ScopedEntity<ParentScope>
    {
        public string Name { get; set; }
    }
}
