namespace Backend.Api.Services.Common
{
    public static class CollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(items);

            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
    }
}
