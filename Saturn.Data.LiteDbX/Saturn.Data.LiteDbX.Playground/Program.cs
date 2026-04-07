// See https://aka.ms/new-console-template for more information

using LiteDbX;

var database = await LiteDatabase.Open("");
var collections = database.GetCollectionNames();
await foreach (var collection in collections)
{
    Console.WriteLine(collection);
}