namespace Bearcat.Website.Shared;

public static class SortableListReordering
{
    public static bool TryMoveItem<TItem>(List<TItem> items, (int OldIndex, int NewIndex) move)
    {
        if (
            move.OldIndex == move.NewIndex
            || move.OldIndex < 0
            || move.OldIndex >= items.Count
            || move.NewIndex < 0
            || move.NewIndex >= items.Count
        )
        {
            return false;
        }

        var item = items[move.OldIndex];
        items.RemoveAt(move.OldIndex);
        items.Insert(move.NewIndex, item);

        return true;
    }
}
