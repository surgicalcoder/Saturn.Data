// ...existing code...
        // Get continuation token from last item (composite: Name and Id)
        var last = firstPageScope1.Last();
        var continueFromToken = $"{{ \"Name\": \"{last.Name}\", \"Id\": \"{last.Id}\" }}";

        // Get second page using composite continuation token
        var secondPageScope1 = await (await ScopedReadonlyRepo.Many<ChildEntity, ParentScope>(
            WELL_KNOWN.Parent_Scope_1.Id,
            e => e.Name.Contains("Scope1"),
            continueFrom: continueFromToken,
            pageSize: 5,
            sortOrders: sortOrders)).ToListAsync();
// ...existing code...

