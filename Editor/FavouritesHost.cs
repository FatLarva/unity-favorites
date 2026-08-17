using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShoreRat.Editor.Favourites
{
    /// <summary>
    /// Shared coordination surface passed to all Favourites subsystems. The window implements
    /// this; subsystems call back through it instead of holding a window reference.
    /// </summary>
    internal interface IFavouritesHost
    {
        FavouritesState Data { get; }
        FavouritesUndoTracker UndoTracker { get; }
        void SaveActive();
        void RefreshList();
        void ActivateEntry(FavouritesItemEntry entry, bool pingOnly);
        void ArmPendingPreview();
        void StartItemDrag(Object[] payload);
        void SetListSelection(int index);
        IEnumerable<int> GetListSelectedIndices();
    }
}
